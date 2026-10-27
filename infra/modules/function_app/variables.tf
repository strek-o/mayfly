variable "name" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "identity_id" {
  type = string
}

variable "identity_client_id" {
  type = string
}

variable "storage_account_name" {
  type = string
}

variable "deployment_container_endpoint" {
  type = string
}

variable "app_settings" {
  type    = map(string)
  default = {}
}

variable "tags" {
  type = map(string)
}
