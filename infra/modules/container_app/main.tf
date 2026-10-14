resource "azurerm_container_app" "this" {
  name                         = "ca-${var.name}"
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_app_environment_id
  revision_mode                = "Single"
  workload_profile_name        = "Consumption"

  identity {
    type         = "UserAssigned"
    identity_ids = compact([var.pull_identity_id, var.app_identity_id])
  }

  registry {
    server   = var.registry_server
    identity = var.pull_identity_id
  }

  ingress {
    external_enabled = true
    target_port      = var.target_port

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    container {
      name   = "web"
      image  = var.image
      cpu    = var.cpu
      memory = var.memory

      dynamic "env" {
        for_each = var.env

        content {
          name  = env.key
          value = env.value
        }
      }

      liveness_probe {
        transport = "HTTP"
        path      = "/healthz"
        port      = var.target_port
      }

      readiness_probe {
        transport = "HTTP"
        path      = "/healthz/ready"
        port      = var.target_port
        timeout   = 5
      }
    }
  }

  tags = var.tags
}
