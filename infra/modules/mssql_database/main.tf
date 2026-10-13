resource "azurerm_mssql_server" "this" {
  name                = "sql-${var.name}"
  resource_group_name = var.resource_group_name
  location            = var.location
  version             = "12.0"

  minimum_tls_version           = "1.2"
  public_network_access_enabled = true

  azuread_administrator {
    login_username              = var.admin_login
    object_id                   = var.admin_object_id
    azuread_authentication_only = true
  }

  tags = var.tags
}

resource "azurerm_mssql_database" "this" {
  name      = "sqldb-${var.name}"
  server_id = azurerm_mssql_server.this.id

  sku_name             = "Basic"
  max_size_gb          = 2
  storage_account_type = "Local"

  tags = var.tags
}
