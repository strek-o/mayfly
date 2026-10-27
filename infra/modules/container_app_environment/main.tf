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

data "azurerm_resource_group" "this" {
  name = var.resource_group_name
}

resource "azapi_resource" "this" {
  type      = "Microsoft.App/managedEnvironments@2026-07-01"
  name      = "cae-${var.name}"
  parent_id = data.azurerm_resource_group.this.id
  location  = var.location
  tags      = var.tags

  schema_validation_enabled = false

  body = {
    properties = {
      environmentMode = "WorkloadProfiles"
      appLogsConfiguration = {
        destination = "azure-monitor"
      }
      workloadProfiles = [
        {
          name                = "Consumption"
          workloadProfileType = "Consumption"
        }
      ]
    }
  }

  response_export_values = ["properties.defaultDomain"]
}

resource "azurerm_monitor_diagnostic_setting" "this" {
  name                           = "log-analytics"
  target_resource_id             = azapi_resource.this.id
  log_analytics_workspace_id     = azurerm_log_analytics_workspace.this.id
  log_analytics_destination_type = "Dedicated"

  enabled_log {
    category = "ContainerAppConsoleLogs"
  }

  enabled_log {
    category = "ContainerAppSystemLogs"
  }
}

moved {
  from = azurerm_container_app_environment.this
  to   = azapi_resource.this
}
