locals {
  github_repository = "strek-o@63954716/mayfly@1413205794"
}

resource "azurerm_user_assigned_identity" "github" {
  for_each = local.environments

  name                = "id-mayfly-github-${each.key}"
  resource_group_name = azurerm_resource_group.bootstrap.name
  location            = azurerm_resource_group.bootstrap.location
  tags                = local.tags
}

resource "azurerm_federated_identity_credential" "github" {
  for_each = local.environments

  name                      = "github-${each.key}"
  user_assigned_identity_id = azurerm_user_assigned_identity.github[each.key].id
  issuer                    = "https://token.actions.githubusercontent.com"
  audience                  = ["api://AzureADTokenExchange"]
  subject                   = "repo:${local.github_repository}:environment:${each.key}"
}
