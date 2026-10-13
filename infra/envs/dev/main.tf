locals {
  environment     = "dev"
  registry_server = "crmayfly.azurecr.io"

  tags = {
    project     = "mayfly"
    environment = local.environment
    managed_by  = "terraform"
  }
}

data "azurerm_resource_group" "env" {
  name = "rg-mayfly-${local.environment}"
}

data "azurerm_user_assigned_identity" "pull" {
  name                = "id-mayfly-pull-${local.environment}"
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
  tags                         = local.tags
}
