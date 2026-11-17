variable "aws_region" {
  type    = string
  default = "eu-central-1"
}

variable "project_name" {
  type    = string
  default = "outbox-loadtest"
  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{2,30}$", var.project_name))
    error_message = "Use 3–31 lowercase letters, digits, or hyphens."
  }
}

variable "allowed_client_cidr" {
  type = string
  validation {
    condition     = can(cidrnetmask(var.allowed_client_cidr)) && endswith(var.allowed_client_cidr, "/32")
    error_message = "Specify your public IPv4 address followed by /32."
  }
}

variable "ssh_public_key_path" {
  type = string
}

variable "application_host_count" {
  type    = number
  default = 2
  validation {
    condition     = var.application_host_count >= 1 && var.application_host_count <= 20 && floor(var.application_host_count) == var.application_host_count
    error_message = "Choose between 1 and 20 application hosts."
  }
}

variable "application_instance_type" {
  type    = string
  default = "t4g.medium"
}

variable "database_instance_type" {
  type    = string
  default = "t4g.large"
}

variable "kafka_instance_type" {
  type    = string
  default = "t4g.large"
}

variable "observability_instance_type" {
  type    = string
  default = "t4g.large"
}

variable "data_volume_size_gib" {
  type    = number
  default = 50
  validation {
    condition     = var.data_volume_size_gib >= 20 && floor(var.data_volume_size_gib) == var.data_volume_size_gib
    error_message = "Data volumes must be at least 20 GiB."
  }
}
