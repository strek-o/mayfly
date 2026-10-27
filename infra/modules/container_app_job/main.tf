resource "azurerm_container_app_job" "this" {
  name                         = "caj-${var.name}"
  resource_group_name          = var.resource_group_name
  location                     = var.location
  container_app_environment_id = var.container_app_environment_id
  workload_profile_name        = "Consumption"
  replica_timeout_in_seconds   = 300
  replica_retry_limit          = 0

  schedule_trigger_config {
    cron_expression = var.cron_expression
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [var.pull_identity_id, var.app_identity_id]
  }

  registry {
    server   = var.registry_server
    identity = var.pull_identity_id
  }

  template {
    container {
      name   = "job"
      image  = var.image
      args   = var.args
      cpu    = 0.25
      memory = "0.5Gi"

      dynamic "env" {
        for_each = var.env

        content {
          name  = env.key
          value = env.value
        }
      }
    }
  }

  tags = var.tags
}
