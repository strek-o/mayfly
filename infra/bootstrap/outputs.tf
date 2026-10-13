output "azure_tenant_id" {
  value = data.azurerm_client_config.current.tenant_id
}

output "azure_subscription_id" {
  value = data.azurerm_client_config.current.subscription_id
}

output "github_client_ids" {
  value = { for env, identity in azurerm_user_assigned_identity.github : env => identity.client_id }
}

output "container_registry_login_server" {
  value = azurerm_container_registry.shared.login_server
}

output "github_build_client_id" {
  value = azurerm_user_assigned_identity.github_build.client_id
}
