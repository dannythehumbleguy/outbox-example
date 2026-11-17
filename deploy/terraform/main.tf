data "aws_availability_zones" "available" {
  state = "available"
}

data "aws_ssm_parameter" "ecs_ami" {
  name = "/aws/service/ecs/optimized-ami/amazon-linux-2023/arm64/recommended/image_id"
}

locals {
  databases = { orders = "orders-db", payment = "payment-db" }
  applications = { for i in range(var.application_host_count) : format("app-%02d", i + 1) => {
    role = "application", instance_type = var.application_instance_type
  } }
  infrastructure = merge({ for service, host in local.databases : host => {
    role = host, instance_type = var.database_instance_type
    } }, {
    kafka         = { role = "kafka", instance_type = var.kafka_instance_type }
    observability = { role = "observability", instance_type = var.observability_instance_type }
  })
  hosts = merge(local.applications, local.infrastructure)
  ecs_user_data = { for name, host in local.hosts : name => templatefile("${path.module}/ecs-init.yml.tftpl", {
    cluster_name = aws_ecs_cluster.lab.name
    workload     = host.role
  }) }
}

resource "aws_vpc" "lab" {
  cidr_block           = "10.42.0.0/16"
  enable_dns_support   = true
  enable_dns_hostnames = true
  tags                 = { Name = var.project_name }
}

resource "aws_subnet" "lab" {
  vpc_id                  = aws_vpc.lab.id
  cidr_block              = "10.42.1.0/24"
  availability_zone       = data.aws_availability_zones.available.names[0]
  map_public_ip_on_launch = true
}

# An ALB requires subnets in two availability zones. Hosts stay in the first.
resource "aws_subnet" "load_balancer" {
  vpc_id                  = aws_vpc.lab.id
  cidr_block              = "10.42.2.0/24"
  availability_zone       = data.aws_availability_zones.available.names[1]
  map_public_ip_on_launch = true
}

resource "aws_internet_gateway" "lab" {
  vpc_id = aws_vpc.lab.id
}

resource "aws_route_table" "lab" {
  vpc_id = aws_vpc.lab.id
  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.lab.id
  }
}

resource "aws_route_table_association" "lab" {
  subnet_id      = aws_subnet.lab.id
  route_table_id = aws_route_table.lab.id
}

resource "aws_route_table_association" "load_balancer" {
  subnet_id      = aws_subnet.load_balancer.id
  route_table_id = aws_route_table.lab.id
}

resource "aws_security_group" "ssh" {
  name_prefix = "${var.project_name}-ssh-"
  vpc_id      = aws_vpc.lab.id
  ingress {
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = [var.allowed_client_cidr]
  }
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "load_balancer" {
  name_prefix = "${var.project_name}-alb-"
  vpc_id      = aws_vpc.lab.id
  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = [var.allowed_client_cidr]
  }
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = [aws_vpc.lab.cidr_block]
  }
}

resource "aws_security_group" "application" {
  name_prefix = "${var.project_name}-application-"
  vpc_id      = aws_vpc.lab.id
  ingress {
    protocol        = "tcp"
    from_port       = 32768
    to_port         = 65535
    security_groups = [aws_security_group.load_balancer.id, aws_security_group.observability.id]
  }
}

resource "aws_security_group" "observability" {
  name_prefix = "${var.project_name}-observability-"
  vpc_id      = aws_vpc.lab.id
  ingress {
    from_port   = 3000
    to_port     = 3000
    protocol    = "tcp"
    cidr_blocks = [var.allowed_client_cidr]
  }
}

# Separate rules avoid a dependency cycle between the two security groups.
resource "aws_vpc_security_group_ingress_rule" "telemetry" {
  for_each                     = toset(["3100", "4317"])
  security_group_id            = aws_security_group.observability.id
  referenced_security_group_id = aws_security_group.application.id
  ip_protocol                  = "tcp"
  from_port                    = tonumber(each.value)
  to_port                      = tonumber(each.value)
}

resource "aws_security_group" "data" {
  for_each    = merge({ for host in values(local.databases) : host => 5432 }, { kafka = 9092 })
  name_prefix = "${var.project_name}-${each.key}-"
  vpc_id      = aws_vpc.lab.id
  ingress {
    from_port       = each.value
    to_port         = each.value
    protocol        = "tcp"
    security_groups = each.key == "kafka" ? [aws_security_group.application.id, aws_security_group.observability.id] : [aws_security_group.application.id]
  }
}

# Client access is separate from the private ports used by application hosts.
resource "aws_vpc_security_group_ingress_rule" "client_data" {
  for_each          = merge({ for host in values(local.databases) : host => 5432 }, { kafka = 9094 })
  security_group_id = aws_security_group.data[each.key].id
  cidr_ipv4         = var.allowed_client_cidr
  ip_protocol       = "tcp"
  from_port         = each.value
  to_port           = each.value
}

resource "aws_security_group" "metrics" {
  name_prefix = "${var.project_name}-metrics-"
  vpc_id      = aws_vpc.lab.id
  ingress {
    from_port       = 9100
    to_port         = 9100
    protocol        = "tcp"
    security_groups = [aws_security_group.observability.id]
  }
}

resource "aws_key_pair" "lab" {
  key_name_prefix = "${var.project_name}-"
  public_key      = file(pathexpand(var.ssh_public_key_path))
}

resource "aws_iam_role" "hosts" {
  name_prefix = "${var.project_name}-"
  assume_role_policy = jsonencode({
    Version   = "2012-10-17"
    Statement = [{ Action = "sts:AssumeRole", Effect = "Allow", Principal = { Service = "ec2.amazonaws.com" } }]
  })
}

resource "aws_iam_role_policy_attachment" "ecs_hosts" {
  role       = aws_iam_role.hosts.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonEC2ContainerServiceforEC2Role"
}

resource "aws_iam_instance_profile" "hosts" {
  name_prefix = "${var.project_name}-"
  role        = aws_iam_role.hosts.name
}

resource "aws_instance" "host" {
  for_each                    = local.hosts
  ami                         = nonsensitive(data.aws_ssm_parameter.ecs_ami.value)
  instance_type               = each.value.instance_type
  subnet_id                   = aws_subnet.lab.id
  key_name                    = aws_key_pair.lab.key_name
  iam_instance_profile        = aws_iam_instance_profile.hosts.name
  depends_on                  = [aws_iam_role_policy_attachment.ecs_hosts]
  user_data                   = local.ecs_user_data[each.key]
  user_data_replace_on_change = true
  vpc_security_group_ids = concat(
    [aws_security_group.ssh.id, aws_security_group.metrics.id],
    each.value.role == "application" ? [aws_security_group.application.id] : [],
    each.value.role == "observability" ? [aws_security_group.observability.id] : [],
    contains(keys(aws_security_group.data), each.key) ? [aws_security_group.data[each.key].id] : []
  )
  metadata_options {
    http_tokens                 = "required"
    http_put_response_hop_limit = 1
  }
  root_block_device {
    volume_type = "gp3"
    volume_size = 30
    encrypted   = true
  }
  dynamic "credit_specification" {
    for_each = startswith(each.value.instance_type, "t") ? [1] : []
    content {
      cpu_credits = "standard"
    }
  }
  tags = { Name = "${var.project_name}-${each.key}", Role = each.value.role }
}

resource "aws_ebs_volume" "data" {
  for_each          = local.infrastructure
  availability_zone = aws_subnet.lab.availability_zone
  size              = var.data_volume_size_gib
  type              = "gp3"
  encrypted         = true
  tags              = { Name = "${var.project_name}-${each.key}-data" }
}

resource "aws_volume_attachment" "data" {
  for_each    = local.infrastructure
  device_name = "/dev/sdf"
  volume_id   = aws_ebs_volume.data[each.key].id
  instance_id = aws_instance.host[each.key].id
}

resource "aws_ecr_repository" "service" {
  for_each             = toset(["orders", "payment"])
  name                 = "${var.project_name}/${each.key}"
  image_tag_mutability = "IMMUTABLE"
  force_delete         = true
  image_scanning_configuration {
    scan_on_push = true
  }
}
