locals {
  services = { orders = "/orders", payment = "/payments" }
  task_definitions = { for name, path in local.services : name => {
    family                  = "${var.project_name}-${name}"
    networkMode             = "bridge"
    requiresCompatibilities = ["EC2"]
    executionRoleArn        = aws_iam_role.execution.arn
    cpu                     = "256"
    memory                  = "512"
    runtimePlatform         = { cpuArchitecture = "ARM64", operatingSystemFamily = "LINUX" }
    placementConstraints    = [{ type = "memberOf", expression = "attribute:workload == application" }]
    containerDefinitions = [{
      name              = name
      image             = "${aws_ecr_repository.service[name].repository_url}:unreleased"
      essential         = true
      cpu               = 256
      memory            = 512
      memoryReservation = 128
      portMappings      = [{ containerPort = 80, hostPort = 0, protocol = "tcp" }]
      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "PathBase", value = path },
        { name = "Database__MigrateOnStartup", value = "false" },
        { name = "Kafka__Brokers", value = "${aws_instance.host["kafka"].private_ip}:9092" },
        { name = "Loki__Url", value = "http://${aws_instance.host["observability"].private_ip}:3100" },
        { name = "Otel__TempoEndpoint", value = "http://${aws_instance.host["observability"].private_ip}:4317" }
      ]
      secrets = [{
        name      = "ConnectionStrings__DefaultConnection"
        valueFrom = aws_secretsmanager_secret.database[name].arn
      }]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.application.name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = name
        }
      }
    }]
  } }
}

resource "aws_ecs_cluster" "lab" {
  name = var.project_name
}

resource "aws_iam_role" "execution" {
  name_prefix = "${var.project_name}-execution-"
  assume_role_policy = jsonencode({
    Version   = "2012-10-17"
    Statement = [{ Action = "sts:AssumeRole", Effect = "Allow", Principal = { Service = "ecs-tasks.amazonaws.com" } }]
  })
}

resource "aws_iam_role_policy_attachment" "execution" {
  role       = aws_iam_role.execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_secretsmanager_secret" "database" {
  for_each                = local.databases
  name_prefix             = "${var.project_name}-${each.key}-database-"
  recovery_window_in_days = 0
}

resource "aws_iam_role_policy" "database_secret" {
  role = aws_iam_role.execution.id
  policy = jsonencode({
    Version   = "2012-10-17"
    Statement = [{ Effect = "Allow", Action = ["secretsmanager:GetSecretValue"], Resource = concat([for secret in aws_secretsmanager_secret.database : secret.arn], [for secret in aws_secretsmanager_secret.infrastructure : secret.arn]) }]
  })
}

resource "aws_cloudwatch_log_group" "application" {
  name              = "/ecs/${var.project_name}"
  retention_in_days = 3
}

resource "aws_lb" "application" {
  name_prefix        = "outbox"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.load_balancer.id]
  subnets            = [aws_subnet.lab.id, aws_subnet.load_balancer.id]
  depends_on         = [aws_route_table_association.lab, aws_route_table_association.load_balancer]
}

resource "aws_lb_target_group" "service" {
  for_each             = local.services
  name_prefix          = substr(each.key, 0, 6)
  port                 = 80
  protocol             = "HTTP"
  target_type          = "instance"
  vpc_id               = aws_vpc.lab.id
  deregistration_delay = 15
  health_check {
    path                = "${each.value}/metrics"
    healthy_threshold   = 2
    unhealthy_threshold = 3
    interval            = 15
    timeout             = 5
    matcher             = "200"
  }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.application.arn
  port              = 80
  protocol          = "HTTP"
  default_action {
    type = "fixed-response"
    fixed_response {
      content_type = "text/plain"
      status_code  = "404"
      message_body = "Not found"
    }
  }
}

resource "aws_lb_listener_rule" "service" {
  for_each     = local.services
  listener_arn = aws_lb_listener.http.arn
  priority     = each.key == "orders" ? 10 : 20
  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.service[each.key].arn
  }
  condition {
    path_pattern {
      values = [each.value, "${each.value}/*"]
    }
  }
}

resource "aws_service_discovery_private_dns_namespace" "application" {
  name = "${var.project_name}.internal"
  vpc  = aws_vpc.lab.id
}

resource "aws_service_discovery_service" "application" {
  for_each = local.services
  name     = each.key
  dns_config {
    namespace_id   = aws_service_discovery_private_dns_namespace.application.id
    routing_policy = "MULTIVALUE"
    dns_records {
      ttl  = 10
      type = "SRV"
    }
  }
}

# Application services start at zero tasks until images are built and migrations pass.
resource "aws_ecs_task_definition" "application" {
  for_each                 = local.task_definitions
  family                   = each.value.family
  network_mode             = each.value.networkMode
  requires_compatibilities = each.value.requiresCompatibilities
  execution_role_arn       = each.value.executionRoleArn
  cpu                      = each.value.cpu
  memory                   = each.value.memory
  container_definitions    = jsonencode(each.value.containerDefinitions)
  placement_constraints {
    type       = "memberOf"
    expression = "attribute:workload == application"
  }
  runtime_platform {
    cpu_architecture        = "ARM64"
    operating_system_family = "LINUX"
  }
}

resource "aws_ecs_service" "application" {
  for_each                           = local.services
  name                               = each.key
  cluster                            = aws_ecs_cluster.lab.id
  task_definition                    = aws_ecs_task_definition.application[each.key].arn
  launch_type                        = "EC2"
  desired_count                      = 0
  force_delete                       = true
  deployment_minimum_healthy_percent = 50
  deployment_maximum_percent         = 100
  health_check_grace_period_seconds  = 60
  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }
  ordered_placement_strategy {
    type  = "spread"
    field = "instanceId"
  }
  placement_constraints {
    type       = "memberOf"
    expression = "attribute:workload == application"
  }
  load_balancer {
    target_group_arn = aws_lb_target_group.service[each.key].arn
    container_name   = each.key
    container_port   = 80
  }
  service_registries {
    registry_arn   = aws_service_discovery_service.application[each.key].arn
    container_name = each.key
    container_port = 80
  }
  lifecycle {
    # Ansible owns the live release and replica counts after initial creation.
    ignore_changes = [task_definition, desired_count]
  }
  depends_on = [aws_lb_listener_rule.service, aws_iam_role_policy_attachment.execution, aws_iam_role_policy_attachment.ecs_hosts, aws_iam_role_policy.database_secret]
}

resource "aws_ecs_task_definition" "node_exporter" {
  family                   = "${var.project_name}-node-exporter"
  network_mode             = "host"
  pid_mode                 = "host"
  requires_compatibilities = ["EC2"]
  execution_role_arn       = aws_iam_role.execution.arn
  runtime_platform {
    cpu_architecture        = "ARM64"
    operating_system_family = "LINUX"
  }
  volume {
    name      = "root"
    host_path = "/"
  }
  container_definitions = jsonencode([{
    name         = "node-exporter"
    image        = "prom/node-exporter:v1.9.1"
    essential    = true
    memory       = 64
    cpu          = 32
    command      = ["--path.rootfs=/host"]
    portMappings = [{ containerPort = 9100, hostPort = 9100, protocol = "tcp" }]
    mountPoints  = [{ sourceVolume = "root", containerPath = "/host", readOnly = true }]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.application.name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = "node-exporter"
      }
    }
  }])
}

resource "aws_ecs_service" "node_exporter" {
  name                = "node-exporter"
  cluster             = aws_ecs_cluster.lab.id
  task_definition     = aws_ecs_task_definition.node_exporter.arn
  launch_type         = "EC2"
  scheduling_strategy = "DAEMON"
  depends_on          = [aws_iam_role_policy_attachment.execution, aws_iam_role_policy_attachment.ecs_hosts]
}
