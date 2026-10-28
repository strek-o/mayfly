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

resource "azurerm_role_assignment" "github_key_vault" {
  for_each = local.environments

  scope                = azurerm_resource_group.env[each.key].id
  role_definition_name = "Key Vault Crypto Officer"
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

resource "azurerm_user_assigned_identity" "pull" {
  for_each = local.environments

  name                = "id-mayfly-pull-${each.key}"
  resource_group_name = azurerm_resource_group.bootstrap.name
  location            = azurerm_resource_group.bootstrap.location
  tags                = merge(local.tags, { environment = each.key })
}

resource "azurerm_role_assignment" "pull_acr" {
  for_each = local.environments

  scope                = azurerm_container_registry.shared.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.pull[each.key].principal_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_role_assignment" "github_pull_operator" {
  for_each = local.environments

  scope                = azurerm_user_assigned_identity.pull[each.key].id
  role_definition_name = "Managed Identity Operator"
  principal_id         = azurerm_user_assigned_identity.github[each.key].principal_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_user_assigned_identity" "app" {
  for_each = local.environments

  name                = "id-mayfly-app-${each.key}"
  resource_group_name = azurerm_resource_group.bootstrap.name
  location            = azurerm_resource_group.bootstrap.location
  tags                = merge(local.tags, { environment = each.key })
}

resource "azurerm_role_assignment" "github_app_operator" {
  for_each = local.environments

  scope                = azurerm_user_assigned_identity.app[each.key].id
  role_definition_name = "Managed Identity Operator"
  principal_id         = azurerm_user_assigned_identity.github[each.key].principal_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_role_assignment" "app_key_vault" {
  for_each = local.environments

  scope                = azurerm_resource_group.env[each.key].id
  role_definition_name = "Key Vault Crypto Service Encryption User"
  principal_id         = azurerm_user_assigned_identity.app[each.key].principal_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_role_assignment" "app_storage" {
  for_each = local.environments

  scope                = azurerm_resource_group.env[each.key].id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.app[each.key].principal_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_role_assignment" "app_monitoring" {
  for_each = local.environments

  scope                = azurerm_resource_group.env[each.key].id
  role_definition_name = "Monitoring Metrics Publisher"
  principal_id         = azurerm_user_assigned_identity.app[each.key].principal_id
  principal_type       = "ServicePrincipal"
}
