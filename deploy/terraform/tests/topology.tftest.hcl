# Mocked provider: these tests never create resources or call AWS.
mock_provider "aws" {
  mock_resource "aws_iam_role" {
    defaults = { arn = "arn:aws:iam::123456789012:role/test-role" }
  }
  mock_resource "aws_lb" {
    defaults = { arn = "arn:aws:elasticloadbalancing:eu-central-1:123456789012:loadbalancer/app/test/0123456789abcdef" }
  }
  mock_resource "aws_lb_listener" {
    defaults = { arn = "arn:aws:elasticloadbalancing:eu-central-1:123456789012:listener/app/test/0123456789abcdef/0123456789abcdef" }
  }
  mock_resource "aws_lb_target_group" {
    defaults = { arn = "arn:aws:elasticloadbalancing:eu-central-1:123456789012:targetgroup/test/0123456789abcdef" }
  }
  mock_resource "aws_service_discovery_service" {
    defaults = { arn = "arn:aws:servicediscovery:eu-central-1:123456789012:service/srv-test" }
  }
}

variables {
  allowed_client_cidr = "203.0.113.1/32"
  ssh_public_key_path = "../.env.example" # Only file readability matters with the mocked key pair.
}

override_data {
  target = data.aws_availability_zones.available
  values = { names = ["eu-central-1a", "eu-central-1b"] }
}

override_data {
  target = data.aws_ssm_parameter.ecs_ami
  values = { value = "ami-0123456789abcdef0" }
}

run "six_hosts" {
  command = plan

  assert {
    condition     = length(aws_instance.host) == 6 && length(local.applications) == 2 && length(local.databases) == 2
    error_message = "The default deployment must contain six hosts, including two application hosts and two database hosts."
  }
  assert {
    condition     = length(aws_ebs_volume.data) == 4 && alltrue([for volume in aws_ebs_volume.data : volume.encrypted && volume.type == "gp3"])
    error_message = "Each supporting host must have a separate encrypted gp3 data volume."
  }
  assert {
    condition     = alltrue([for rule in aws_security_group.ssh.ingress : rule.from_port == 22 && length(rule.cidr_blocks) == 1 && contains(rule.cidr_blocks, "203.0.113.1/32")])
    error_message = "SSH must be limited to the configured client address."
  }
  assert {
    condition     = alltrue([for rule in aws_security_group.load_balancer.ingress : rule.from_port == 80 && length(rule.cidr_blocks) == 1 && contains(rule.cidr_blocks, "203.0.113.1/32")])
    error_message = "Application HTTP must be limited to the configured client address."
  }
  assert {
    condition     = alltrue(flatten([for group in aws_security_group.data : [for rule in group.ingress : try(length(rule.cidr_blocks), 0) == 0]]))
    error_message = "Application access to database and Kafka must retain private security-group rules."
  }
  assert {
    condition     = length(aws_security_group.observability.ingress) == 1 && alltrue([for rule in aws_security_group.observability.ingress : rule.from_port == 3000 && rule.to_port == 3000 && rule.protocol == "tcp" && length(rule.cidr_blocks) == 1 && contains(rule.cidr_blocks, var.allowed_client_cidr)])
    error_message = "Grafana HTTP must accept only the configured client IP."
  }
  assert {
    condition = length(aws_vpc_security_group_ingress_rule.client_data) == 3 && alltrue([
      for name, rule in aws_vpc_security_group_ingress_rule.client_data :
      rule.cidr_ipv4 == var.allowed_client_cidr && rule.ip_protocol == "tcp" &&
      rule.from_port == { orders-db = 5432, payment-db = 5432, kafka = 9094 }[name] && rule.to_port == rule.from_port
    ])
    error_message = "Database and external Kafka client ports must accept only the configured client IP."
  }
  assert {
    condition     = alltrue([for rule in aws_security_group.application.ingress : rule.from_port == 32768 && rule.to_port == 65535 && try(length(rule.cidr_blocks), 0) == 0])
    error_message = "Application task ports must accept traffic only from security groups, not public CIDRs."
  }
  assert {
    condition     = alltrue([for service in aws_ecs_service.application : service.launch_type == "EC2" && service.desired_count == 0 && service.deployment_circuit_breaker[0].rollback])
    error_message = "ECS services must start at zero tasks until images and migrations are ready, with rollback enabled."
  }
  assert {
    condition     = alltrue([for task in aws_ecs_task_definition.application : task.network_mode == "bridge" && task.runtime_platform[0].cpu_architecture == "ARM64"])
    error_message = "ECS tasks must support multiple ARM64 service replicas on each host."
  }
  assert {
    condition     = alltrue([for host in aws_instance.host : host.ami == "ami-0123456789abcdef0"])
    error_message = "All six hosts must use the ECS-optimized ARM64 image."
  }
  assert {
    condition     = alltrue([for name, service in aws_ecs_service.infrastructure : one(service.placement_constraints).expression == "attribute:workload == ${name}" && service.deployment_minimum_healthy_percent == 0 && service.deployment_maximum_percent == 100 && service.desired_count == 0])
    error_message = "Infrastructure must stay on its dedicated host, start after preparation, and update with only one writer."
  }
  assert {
    condition     = alltrue([for task in aws_ecs_task_definition.application : one(task.placement_constraints).expression == "attribute:workload == application"]) && local.task_definitions.orders.placementConstraints[0].expression == "attribute:workload == application"
    error_message = "Application services and one-off migration tasks must stay off infrastructure hosts."
  }
  assert {
    condition     = alltrue([for task in local.database_tasks : task.volumes.postgres == "/data/postgres"]) && local.infrastructure_tasks.kafka.volumes.kafka == "/data/kafka" && local.infrastructure_tasks.observability.volumes.prometheus == "/data/prometheus"
    error_message = "Stateful task bind mounts must use the prepared EBS data directory."
  }
  assert {
    condition = alltrue([for name in keys(local.infrastructure) : length([
      for file in yamldecode(local.ecs_user_data[name]).write_files : file
      if endswith(file.path, "/storage.conf") && strcontains(file.content, "RequiresMountsFor=/data") && strcontains(file.content, "ConditionPathIsMountPoint=/data") && strcontains(file.content, "ConditionPathExists=/opt/outbox/.storage-ready")
    ]) == 2])
    error_message = "Docker and ECS must wait for EBS mounting and configuration before starting on supporting hosts."
  }
  assert {
    condition     = startswith(yamldecode(file("../../devops/grafana/provisioning/dashboards/dashboards.yml")).providers[0].options.path, one([for mount in local.infrastructure_tasks.observability.containers[3].mountPoints : mount if mount.sourceVolume == "grafana_config"]).containerPath)
    error_message = "Grafana's provisioned dashboard path must be inside its configuration bind mount."
  }
  assert {
    condition     = aws_ecs_service.node_exporter.scheduling_strategy == "DAEMON" && aws_ecs_task_definition.node_exporter.runtime_platform[0].cpu_architecture == "ARM64"
    error_message = "Every ECS host must receive a node-exporter task."
  }
  assert {
    condition     = alltrue([for host in aws_instance.host : host.metadata_options[0].http_tokens == "required"])
    error_message = "Every host must require IMDSv2."
  }
}

run "additional_application_host" {
  command = plan
  variables {
    application_host_count = 3
  }
  assert {
    condition     = length(aws_instance.host) == 7 && length(aws_ebs_volume.data) == 4
    error_message = "Adding an application host must not duplicate supporting infrastructure."
  }
}

run "direct_client_access" {
  command = apply

  override_resource {
    target = aws_instance.host["kafka"]
    values = { private_ip = "10.42.1.20", public_ip = "203.0.113.20" }
  }

  assert {
    condition = alltrue([
      for name, rule in aws_vpc_security_group_ingress_rule.client_data :
      rule.security_group_id == aws_security_group.data[name].id
    ])
    error_message = "Each client ingress rule must belong to its dedicated data host."
  }
  assert {
    condition = (
      contains(one(aws_security_group.data["kafka"].ingress).security_groups, aws_security_group.observability.id) &&
      contains(local.infrastructure_tasks.observability.containers[2].links, "kafka-exporter:kafka-exporter") &&
      one([for container in local.infrastructure_tasks.observability.containers : container if container.name == "kafka-exporter"]).command == ["--kafka.server=${aws_instance.host["kafka"].private_ip}:9092"]
    )
    error_message = "The observability task must reach Kafka and expose exporter metrics to Prometheus."
  }
  assert {
    condition = (
      one([for entry in local.infrastructure_tasks.kafka.containers[0].environment : entry.value if entry.name == "KAFKA_ADVERTISED_LISTENERS"]) == "INTERNAL://10.42.1.20:9092,EXTERNAL://203.0.113.20:9094" &&
      one([for entry in local.infrastructure_tasks.kafka.containers[0].environment : entry.value if entry.name == "KAFKA_LISTENERS"]) == "INTERNAL://:9092,EXTERNAL://:9094,CONTROLLER://:9093" &&
      one([for entry in local.infrastructure_tasks.kafka.containers[0].environment : entry.value if entry.name == "KAFKA_LISTENER_SECURITY_PROTOCOL_MAP"]) == "CONTROLLER:PLAINTEXT,INTERNAL:PLAINTEXT,EXTERNAL:PLAINTEXT" &&
      toset([for mapping in local.infrastructure_tasks.kafka.containers[0].portMappings : mapping.hostPort if mapping.hostPort == mapping.containerPort]) == toset([9092, 9094])
    )
    error_message = "Kafka must publish a reachable public client listener and preserve its private application listener."
  }
  assert {
    condition = (
      output.deployment.grafana_url == "http://${aws_instance.host["observability"].public_ip}:3000" &&
      one([for entry in local.infrastructure_tasks.observability.containers[3].environment : entry.value if entry.name == "GF_SERVER_ROOT_URL"]) == output.deployment.grafana_url &&
      alltrue([for service, host in local.databases : output.deployment.database_endpoints[service] == "${aws_instance.host[host].public_ip}:5432"]) &&
      output.deployment.kafka_endpoint == "203.0.113.20:9094"
    )
    error_message = "Connection outputs must use public addresses and the exposed client ports."
  }
  assert {
    condition = (
      aws_instance.host["orders-db"].id != aws_instance.host["payment-db"].id &&
      aws_ebs_volume.data["orders-db"].id != aws_ebs_volume.data["payment-db"].id &&
      length(output.inventory.all.children.databases.hosts) == 2 &&
      alltrue([for service, host in local.databases :
        output.deployment.databases[service].host == aws_instance.host[host].private_ip &&
        output.deployment.databases[service].name == service &&
        local.task_definitions[service].containerDefinitions[0].secrets[0].valueFrom == aws_secretsmanager_secret.database[service].arn &&
        one([for entry in local.database_tasks[host].containers[0].environment : entry.value if entry.name == "POSTGRES_DB"]) == service &&
        local.database_tasks[host].containers[0].secrets[0].valueFrom == aws_secretsmanager_secret.infrastructure["${service}_database_password"].arn &&
        local.database_tasks[host].containers[1].secrets[0].valueFrom == aws_secretsmanager_secret.infrastructure["${service}_database_password"].arn
      ]) &&
      aws_secretsmanager_secret.database["orders"].arn != aws_secretsmanager_secret.database["payment"].arn &&
      aws_secretsmanager_secret.infrastructure["orders_database_password"].arn != aws_secretsmanager_secret.infrastructure["payment_database_password"].arn
    )
    error_message = "Each service must use its own PostgreSQL host, EBS volume, database, and secrets."
  }
}
