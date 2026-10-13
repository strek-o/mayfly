output "id" {
  value = azurerm_container_app.this.id
}

output "url" {
  value = "https://${azurerm_container_app.this.ingress[0].fqdn}"
}
