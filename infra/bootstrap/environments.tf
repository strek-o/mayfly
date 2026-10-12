resource "azurerm_resource_provider_registration" "container_apps" {
  name = "Microsoft.App"
}

resource "azurerm_resource_group" "env" {
  for_each = local.environments

  name     = "rg-mayfly-${each.key}"
  location = var.location
  tags     = merge(local.tags, { environment = each.key })
}

resource "azurerm_role_assignment" "github_contributor" {
  for_each = local.environments

  scope                = azurerm_resource_group.env[each.key].id
  role_definition_name = "Contributor"
  principal_id         = azurerm_user_assigned_identity.github[each.key].principal_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_storage_container" "env" {
  for_each = local.environments

  name                  = "tfstate-${each.key}"
  storage_account_id    = azurerm_storage_account.tfstate.id
  container_access_type = "private"
}

resource "azurerm_role_assignment" "github_state" {
  for_each = local.environments

  scope                = azurerm_storage_container.env[each.key].id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.github[each.key].principal_id
  principal_type       = "ServicePrincipal"
}
