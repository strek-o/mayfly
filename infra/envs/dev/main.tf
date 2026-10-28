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
    APP_VERSION                    = var.image_tag
    AZURE_CLIENT_ID                = data.azurerm_user_assigned_identity.app.client_id
    ConnectionStrings__Mayfly      = "Server=tcp:${module.mssql_database.server_fqdn},1433;Database=${module.mssql_database.database_name};Authentication=Active Directory Managed Identity;User Id=${data.azurerm_user_assigned_identity.app.client_id};Encrypt=True"
    DataProtection__KeyVaultKeyUri = azurerm_key_vault_key.data_protection.versionless_id
    Storage__BlobEndpoint          = module.storage_account.blob_endpoint
  }
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
