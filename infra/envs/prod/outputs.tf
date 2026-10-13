output "resource_group_name" {
  value = data.azurerm_resource_group.env.name
}

output "location" {
  value = data.azurerm_resource_group.env.location
}

output "app_url" {
  value = module.container_app.url
}
