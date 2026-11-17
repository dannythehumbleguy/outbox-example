output "inventory" {
  value = {
    all = {
      vars = { ansible_user = "ec2-user", ansible_python_interpreter = "/usr/bin/python3" }
      hosts = { for name, host in aws_instance.host : name => {
        ansible_host   = host.public_ip
        private_ip     = host.private_ip
        data_volume_id = try(aws_ebs_volume.data[name].id, "")
      } }
      children = {
        applications   = { hosts = { for name in keys(local.applications) : name => {} } }
        infrastructure = { hosts = { for name in keys(local.infrastructure) : name => {} } }
        databases      = { hosts = { for host in values(local.databases) : host => {} } }
        kafka          = { hosts = { kafka = {} } }
        observability  = { hosts = { observability = {} } }
      }
    }
  }
}

output "deployment" {
  value = {
    aws_region         = var.aws_region
    ecr_registry       = split("/", aws_ecr_repository.service["orders"].repository_url)[0]
    orders_repository  = aws_ecr_repository.service["orders"].repository_url
    payment_repository = aws_ecr_repository.service["payment"].repository_url
    databases = { for service, host in local.databases : service => {
      host   = aws_instance.host[host].private_ip
      name   = service
      secret = aws_secretsmanager_secret.database[service].arn
    } }
    kafka_ip                   = aws_instance.host["kafka"].private_ip
    observability_ip           = aws_instance.host["observability"].private_ip
    grafana_url                = "http://${aws_instance.host["observability"].public_ip}:3000"
    database_endpoints         = { for service, host in local.databases : service => "${aws_instance.host[host].public_ip}:5432" }
    kafka_endpoint             = "${aws_instance.host["kafka"].public_ip}:9094"
    base_url                   = "http://${aws_lb.application.dns_name}"
    ecs_cluster                = aws_ecs_cluster.lab.name
    discovery_domain           = aws_service_discovery_private_dns_namespace.application.name
    task_definitions           = local.task_definitions
    infrastructure_definitions = { for name, task in aws_ecs_task_definition.infrastructure : name => task.arn }
    infrastructure_secrets     = { for name, secret in aws_secretsmanager_secret.infrastructure : name => secret.arn }
  }
}
