output "azure_tenant_id" {
  value = data.azurerm_client_config.current.tenant_id
}

output "azure_subscription_id" {
  value = data.azurerm_client_config.current.subscription_id
}

output "github_client_ids" {
  value = { for env, identity in azurerm_user_assigned_identity.github : env => identity.client_id }
}
