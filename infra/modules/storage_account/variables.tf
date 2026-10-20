variable "name" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "container_names" {
  type = list(string)
}

variable "cors_allowed_origins" {
  type = list(string)
}

variable "tags" {
  type    = map(string)
  default = {}
}
