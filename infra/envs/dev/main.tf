locals {
  environment     = "dev"
  registry_server = "crmayfly.azurecr.io"

  tags = {
    project     = "mayfly"
    environment = local.environment
    managed_by  = "terraform"
  }
}

data "azurerm_client_config" "current" {}

data "azurerm_resource_group" "env" {
  name = "rg-mayfly-${local.environment}"
}

data "azurerm_user_assigned_identity" "pull" {
  name                = "id-mayfly-pull-${local.environment}"
  resource_group_name = "rg-mayfly-bootstrap"
}

data "azurerm_user_assigned_identity" "app" {
  name                = "id-mayfly-app-${local.environment}"
  resource_group_name = "rg-mayfly-bootstrap"
}

module "container_app_environment" {
  source = "../../modules/container_app_environment"

  name                = "mayfly-${local.environment}"
  resource_group_name = data.azurerm_resource_group.env.name
  location            = data.azurerm_resource_group.env.location
  tags                = local.tags
}

module "container_app" {
  source = "../../modules/container_app"

  name                         = "mayfly-${local.environment}"
  resource_group_name          = data.azurerm_resource_group.env.name
  container_app_environment_id = module.container_app_environment.id
  image                        = "${local.registry_server}/mayfly:${var.image_tag}"
  registry_server              = local.registry_server
  pull_identity_id             = data.azurerm_user_assigned_identity.pull.id
  app_identity_id              = data.azurerm_user_assigned_identity.app.id
  tags                         = local.tags

  env = {
    APP_VERSION                           = var.image_tag
    APPLICATIONINSIGHTS_CONNECTION_STRING = azurerm_application_insights.this.connection_string
    AZURE_CLIENT_ID                       = data.azurerm_user_assigned_identity.app.client_id
    ConnectionStrings__Mayfly             = "Server=tcp:${module.mssql_database.server_fqdn},1433;Database=${module.mssql_database.database_name};Authentication=Active Directory Managed Identity;User Id=${data.azurerm_user_assigned_identity.app.client_id};Encrypt=True"
    DataProtection__KeyVaultKeyUri        = azurerm_key_vault_key.data_protection.versionless_id
    Storage__BlobEndpoint                 = module.storage_account.blob_endpoint
  }
}

resource "azurerm_application_insights" "this" {
  name                = "appi-mayfly-${local.environment}"
  resource_group_name = data.azurerm_resource_group.env.name
  location            = data.azurerm_resource_group.env.location
  workspace_id        = module.container_app_environment.log_analytics_workspace_id
  application_type    = "web"
  retention_in_days   = 30

  local_authentication_enabled = false

  tags = local.tags
}

data "azurerm_role_definition" "owner" {
  name  = "Owner"
  scope = data.azurerm_resource_group.env.id
}

resource "azurerm_monitor_action_group" "owners" {
  name                = "ag-mayfly-${local.environment}"
  resource_group_name = data.azurerm_resource_group.env.name
  short_name          = "mayfly-${local.environment}"

  arm_role_receiver {
    name                    = "owners"
    role_id                 = data.azurerm_role_definition.owner.role_definition_id
    use_common_alert_schema = true
  }

  tags = local.tags
}

resource "azurerm_monitor_metric_alert" "server_errors" {
  name                = "mayfly-${local.environment}-server-errors"
  resource_group_name = data.azurerm_resource_group.env.name
  scopes              = [azurerm_application_insights.this.id]
  description         = "The app answered requests with a 5xx status code."
  severity            = 2

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "requests/count"
    aggregation      = "Count"
    operator         = "GreaterThan"
    threshold        = 0

    dimension {
      name     = "request/resultCode"
      operator = "StartsWith"
      values   = ["5"]
    }
  }

  action {
    action_group_id = azurerm_monitor_action_group.owners.id
  }

  tags = local.tags
}

resource "azurerm_monitor_metric_alert" "replica_restarts" {
  name                = "mayfly-${local.environment}-replica-restarts"
  resource_group_name = data.azurerm_resource_group.env.name
  scopes              = [module.container_app.id]
  description         = "A replica of the container app restarted."
  severity            = 1

  criteria {
    metric_namespace = "Microsoft.App/containerApps"
    metric_name      = "RestartCount"
    aggregation      = "Maximum"
    operator         = "GreaterThan"
    threshold        = 0
  }

  action {
    action_group_id = azurerm_monitor_action_group.owners.id
  }

  tags = local.tags
}

module "storage_account" {
  source = "../../modules/storage_account"

  name                 = "mayfly-${local.environment}"
  resource_group_name  = data.azurerm_resource_group.env.name
  location             = data.azurerm_resource_group.env.location
  container_names      = ["files"]
  cors_allowed_origins = ["https://ca-mayfly-${local.environment}.${module.container_app_environment.default_domain}"]
  tags                 = local.tags
}

module "key_vault" {
  source = "../../modules/key_vault"

  name                = "mayfly-${local.environment}"
  resource_group_name = data.azurerm_resource_group.env.name
  location            = data.azurerm_resource_group.env.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  tags                = local.tags
}

resource "azurerm_key_vault_key" "data_protection" {
  name         = "data-protection"
  key_vault_id = module.key_vault.id
  key_type     = "RSA"
  key_size     = 2048
  key_opts     = ["wrapKey", "unwrapKey"]
}

module "mssql_database" {
  source = "../../modules/mssql_database"

  name                = "mayfly-${local.environment}"
  resource_group_name = data.azurerm_resource_group.env.name
  location            = data.azurerm_resource_group.env.location
  admin_login         = "id-mayfly-github-${local.environment}"
  admin_object_id     = data.azurerm_client_config.current.object_id
  tags                = local.tags
}
