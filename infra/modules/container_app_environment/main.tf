resource "azurerm_log_analytics_workspace" "this" {
  name                = "log-${var.name}"
  resource_group_name = var.resource_group_name
  location            = var.location

  sku                          = "PerGB2018"
  retention_in_days            = var.log_retention_days
  daily_quota_gb               = var.log_daily_quota_gb
  local_authentication_enabled = false

  tags = var.tags
}

resource "azurerm_container_app_environment" "this" {
  name                = "cae-${var.name}"
  resource_group_name = var.resource_group_name
  location            = var.location
  logs_destination    = "azure-monitor"

  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }

  tags = var.tags
}

resource "azurerm_monitor_diagnostic_setting" "this" {
  name                           = "log-analytics"
  target_resource_id             = azurerm_container_app_environment.this.id
  log_analytics_workspace_id     = azurerm_log_analytics_workspace.this.id
  log_analytics_destination_type = "Dedicated"

  enabled_log {
    category = "ContainerAppConsoleLogs"
  }

  enabled_log {
    category = "ContainerAppSystemLogs"
  }
}

removed {
  from = azapi_resource.this

  lifecycle {
    destroy = false
  }
}
