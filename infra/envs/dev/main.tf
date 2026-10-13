locals {
  environment = "dev"

  tags = {
    project     = "mayfly"
    environment = local.environment
    managed_by  = "terraform"
  }
}

data "azurerm_resource_group" "env" {
  name = "rg-mayfly-${local.environment}"
}

module "container_app_environment" {
  source = "../../modules/container_app_environment"

  name                = "mayfly-${local.environment}"
  resource_group_name = data.azurerm_resource_group.env.name
  location            = data.azurerm_resource_group.env.location
  tags                = local.tags
}
