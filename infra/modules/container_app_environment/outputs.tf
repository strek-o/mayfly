output "id" {
  value = azapi_resource.this.id
}

output "default_domain" {
  value = azapi_resource.this.output.properties.defaultDomain
}

output "log_analytics_workspace_id" {
  value = azurerm_log_analytics_workspace.this.id
}
