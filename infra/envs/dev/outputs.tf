output "resource_group_name" {
  value = data.azurerm_resource_group.env.name
}

output "location" {
  value = data.azurerm_resource_group.env.location
}

output "app_url" {
  value = module.container_app.url
}

output "image_tag" {
  value = var.image_tag
}

output "sql_server_name" {
  value = module.mssql_database.server_name
}

output "sql_server_fqdn" {
  value = module.mssql_database.server_fqdn
}

output "sql_database_name" {
  value = module.mssql_database.database_name
}

output "storage_account_name" {
  value = module.storage_account.name
}

output "app_identity_name" {
  value = data.azurerm_user_assigned_identity.app.name
}

output "app_identity_client_id" {
  value = data.azurerm_user_assigned_identity.app.client_id
}
