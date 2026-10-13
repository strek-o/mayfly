variable "name" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "log_retention_days" {
  type    = number
  default = 30
}

variable "log_daily_quota_gb" {
  type    = number
  default = 0.1
}

variable "tags" {
  type    = map(string)
  default = {}
}
