variable "name" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "container_app_environment_id" {
  type = string
}

variable "image" {
  type = string
}

variable "registry_server" {
  type = string
}

variable "pull_identity_id" {
  type = string
}

variable "app_identity_id" {
  type = string
}

variable "cron_expression" {
  type = string
}

variable "args" {
  type = list(string)
}

variable "env" {
  type    = map(string)
  default = {}
}

variable "tags" {
  type    = map(string)
  default = {}
}
