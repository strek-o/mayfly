resource "azurerm_container_registry" "shared" {
  name                = "crmayfly"
  resource_group_name = azurerm_resource_group.bootstrap.name
  location            = azurerm_resource_group.bootstrap.location

  sku           = "Basic"
  admin_enabled = false

  tags = local.tags
}
