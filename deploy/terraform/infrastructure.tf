locals {
  database_tasks = { for service, host in local.databases : host => {
    cpu     = "1536", memory = "3072"
    volumes = { postgres = "/data/postgres" }
    containers = [
      {
        name        = "postgres", image = "postgres:17", memory = 2048, stopTimeout = 120, startTimeout = 180
        environment = [{ name = "POSTGRES_USER", value = "postgres" }, { name = "POSTGRES_DB", value = service }]
        secrets     = [{ name = "POSTGRES_PASSWORD", valueFrom = aws_secretsmanager_secret.infrastructure["${service}_database_password"].arn }]
        mountPoints = [{ sourceVolume = "postgres", containerPath = "/var/lib/postgresql/data", readOnly = false }]
        healthCheck = {
          command  = ["CMD-SHELL", "pg_isready -U postgres -d ${service}"]
          interval = 10, timeout = 5, retries = 6, startPeriod = 60
        }
      },
      {
        name         = "pgbouncer", image = "edoburu/pgbouncer:v1.25.1-p0", memory = 256
        links        = ["postgres:postgres"]
        dependsOn    = [{ containerName = "postgres", condition = "HEALTHY" }]
        startTimeout = 180
        portMappings = [{ containerPort = 5432, hostPort = 5432, protocol = "tcp" }]
        environment = [for name, value in {
          DB_HOST   = "postgres", DB_USER = "postgres", DB_NAME = service
          POOL_MODE = "transaction", MAX_CLIENT_CONN = "1000", DEFAULT_POOL_SIZE = "20"
          AUTH_TYPE = "scram-sha-256", IGNORE_STARTUP_PARAMETERS = "extra_float_digits"
        } : { name = name, value = value }]
        secrets = [{ name = "DB_PASSWORD", valueFrom = aws_secretsmanager_secret.infrastructure["${service}_database_password"].arn }]
        healthCheck = {
          command  = ["CMD-SHELL", "pg_isready -h localhost -p 5432 -U postgres"]
          interval = 10, timeout = 5, retries = 6, startPeriod = 60
        }
      }
    ]
  } }
  infrastructure_tasks = merge(local.database_tasks, {
    kafka = {
      cpu     = "1024", memory = "2048"
      volumes = { kafka = "/data/kafka" }
      containers = [{
        name = "kafka", image = "apache/kafka:3.9.0", memory = 2048, stopTimeout = 120
        portMappings = [
          { containerPort = 9092, hostPort = 9092, protocol = "tcp" },
          { containerPort = 9094, hostPort = 9094, protocol = "tcp" }
        ]
        mountPoints = [{ sourceVolume = "kafka", containerPath = "/var/lib/kafka/data", readOnly = false }]
        environment = [for name, value in {
          CLUSTER_ID                             = "MkU3OEVBNTcwNTJENDM2Qk", KAFKA_NODE_ID = "1", KAFKA_PROCESS_ROLES = "broker,controller"
          KAFKA_LISTENERS                        = "INTERNAL://:9092,EXTERNAL://:9094,CONTROLLER://:9093"
          KAFKA_ADVERTISED_LISTENERS             = "INTERNAL://${aws_instance.host["kafka"].private_ip}:9092,EXTERNAL://${aws_instance.host["kafka"].public_ip}:9094"
          KAFKA_CONTROLLER_LISTENER_NAMES        = "CONTROLLER"
          KAFKA_LISTENER_SECURITY_PROTOCOL_MAP   = "CONTROLLER:PLAINTEXT,INTERNAL:PLAINTEXT,EXTERNAL:PLAINTEXT"
          KAFKA_INTER_BROKER_LISTENER_NAME       = "INTERNAL", KAFKA_CONTROLLER_QUORUM_VOTERS = "1@localhost:9093"
          KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR = "1", KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR = "1"
          KAFKA_TRANSACTION_STATE_LOG_MIN_ISR    = "1", KAFKA_NUM_PARTITIONS = "5"
          KAFKA_LOG_DIRS                         = "/var/lib/kafka/data", KAFKA_LOG_RETENTION_HOURS = "24", KAFKA_HEAP_OPTS = "-Xms512m -Xmx1g"
        } : { name = name, value = value }]
        healthCheck = {
          command  = ["CMD-SHELL", "/opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 --list"]
          interval = 15, timeout = 10, retries = 6, startPeriod = 60
        }
      }]
    }
    observability = {
      # Ansible restarts this task when the mounted configuration files change.
      cpu = "1536", memory = "6144"
      volumes = {
        loki   = "/data/loki", tempo = "/data/tempo", prometheus = "/data/prometheus", grafana = "/data/grafana"
        config = "/opt/outbox", grafana_config = "/opt/outbox/grafana"
      }
      containers = [
        {
          name         = "loki", image = "grafana/loki:3.6.0", memory = 1024
          command      = ["-config.file=/config/loki.yml"]
          portMappings = [{ containerPort = 3100, hostPort = 3100, protocol = "tcp" }]
          mountPoints = [
            { sourceVolume = "loki", containerPath = "/loki", readOnly = false },
            { sourceVolume = "config", containerPath = "/config", readOnly = true }
          ]
        },
        {
          name         = "tempo", image = "grafana/tempo:2.7.2", memory = 2048
          command      = ["-config.file=/config/tempo.yml"]
          portMappings = [{ containerPort = 4317, hostPort = 4317, protocol = "tcp" }]
          mountPoints = [
            { sourceVolume = "tempo", containerPath = "/var/tempo", readOnly = false },
            { sourceVolume = "config", containerPath = "/config", readOnly = true }
          ]
        },
        {
          name      = "prometheus", image = "prom/prometheus:v3.10.0", memory = 2048, cpu = 512
          command   = ["--config.file=/config/prometheus.yml", "--storage.tsdb.retention.time=24h", "--storage.tsdb.retention.size=10GB"]
          links     = ["kafka-exporter:kafka-exporter"]
          dependsOn = [{ containerName = "kafka-exporter", condition = "START" }]
          mountPoints = [
            { sourceVolume = "prometheus", containerPath = "/prometheus", readOnly = false },
            { sourceVolume = "config", containerPath = "/config", readOnly = true }
          ]
        },
        {
          name         = "grafana", image = "grafana/grafana:12.3.0", memory = 512
          links        = ["loki:loki", "tempo:tempo", "prometheus:prometheus"]
          dependsOn    = [for name in ["loki", "tempo", "prometheus"] : { containerName = name, condition = "START" }]
          portMappings = [{ containerPort = 3000, hostPort = 3000, protocol = "tcp" }]
          environment = [
            { name = "GF_SECURITY_ADMIN_USER", value = "admin" },
            { name = "GF_USERS_ALLOW_SIGN_UP", value = "false" },
            { name = "GF_SERVER_ROOT_URL", value = "http://${aws_instance.host["observability"].public_ip}:3000" }
          ]
          secrets = [{ name = "GF_SECURITY_ADMIN_PASSWORD", valueFrom = aws_secretsmanager_secret.infrastructure["grafana_password"].arn }]
          mountPoints = [
            { sourceVolume = "grafana", containerPath = "/var/lib/grafana", readOnly = false },
            { sourceVolume = "grafana_config", containerPath = "/etc/grafana/provisioning", readOnly = true }
          ]
          healthCheck = {
            command  = ["CMD-SHELL", "curl -fsS --max-time 2 http://loki:3100/ready >/dev/null && curl -fsS --max-time 2 http://tempo:3200/ready >/dev/null && curl -fsS --max-time 2 http://prometheus:9090/-/ready >/dev/null && curl -fsS --max-time 2 http://localhost:3000/api/health >/dev/null"]
            interval = 15, timeout = 10, retries = 6, startPeriod = 60
          }
        },
        {
          name      = "kafka-exporter", image = "danielqsj/kafka-exporter:v1.9.0", memory = 128
          essential = false
          command   = ["--kafka.server=${aws_instance.host["kafka"].private_ip}:9092"]
        }
      ]
    }
  })
}

resource "aws_secretsmanager_secret" "infrastructure" {
  for_each                = toset(["orders_database_password", "payment_database_password", "grafana_password"])
  name_prefix             = "${var.project_name}-${each.key}-"
  recovery_window_in_days = 0
}

resource "aws_ecs_task_definition" "infrastructure" {
  for_each                 = local.infrastructure_tasks
  family                   = "${var.project_name}-${each.key}"
  network_mode             = "bridge"
  requires_compatibilities = ["EC2"]
  execution_role_arn       = aws_iam_role.execution.arn
  cpu                      = each.value.cpu
  memory                   = each.value.memory
  placement_constraints {
    type       = "memberOf"
    expression = "attribute:workload == ${each.key}"
  }
  runtime_platform {
    cpu_architecture        = "ARM64"
    operating_system_family = "LINUX"
  }
  dynamic "volume" {
    for_each = each.value.volumes
    content {
      name      = volume.key
      host_path = volume.value
    }
  }
  container_definitions = jsonencode([for container in each.value.containers : merge({
    essential = true
    cpu       = 128
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.application.name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = container.name
      }
    }
  }, container)])
}

# Start only after Ansible has mounted storage and written configuration.
# Stop the old task before starting its replacement: these volumes have one writer.
resource "aws_ecs_service" "infrastructure" {
  for_each                           = local.infrastructure_tasks
  name                               = each.key
  cluster                            = aws_ecs_cluster.lab.id
  task_definition                    = aws_ecs_task_definition.infrastructure[each.key].arn
  launch_type                        = "EC2"
  desired_count                      = 0
  force_delete                       = true
  deployment_minimum_healthy_percent = 0
  deployment_maximum_percent         = 100
  placement_constraints {
    type       = "memberOf"
    expression = "attribute:workload == ${each.key}"
  }
  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }
  lifecycle {
    # Ansible starts these services after preparing their storage and configuration.
    ignore_changes = [desired_count, task_definition]
  }
  depends_on = [aws_iam_role_policy_attachment.execution, aws_iam_role_policy_attachment.ecs_hosts, aws_iam_role_policy.database_secret, aws_volume_attachment.data]
}
