resource "azurerm_log_analytics_workspace" "this" {
  name                = "log-${var.name}"
  resource_group_name = var.resource_group_name
  location            = var.location

  sku               = "PerGB2018"
  retention_in_days = var.log_retention_days
  daily_quota_gb    = var.log_daily_quota_gb

  tags = var.tags
}

resource "azurerm_container_app_environment" "this" {
  name                = "cae-${var.name}"
  resource_group_name = var.resource_group_name
  location            = var.location

  logs_destination           = "log-analytics"
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id

  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }

  tags = var.tags
}

removed {
  from = azapi_resource.this

  lifecycle {
    destroy = false
  }
}
