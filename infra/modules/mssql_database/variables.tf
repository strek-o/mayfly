variable "name" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "admin_login" {
  type = string
}

variable "admin_object_id" {
  type = string
}

variable "tags" {
  type    = map(string)
  default = {}
}
