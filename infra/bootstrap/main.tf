resource "azurerm_resource_group" "bootstrap" {
  name     = "rg-mayfly-bootstrap"
  location = var.location

  tags = {
    project    = "mayfly"
    managed_by = "terraform"
  }
}
