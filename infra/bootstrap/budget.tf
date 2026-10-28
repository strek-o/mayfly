resource "azurerm_consumption_budget_subscription" "monthly" {
  name            = "mayfly-monthly"
  subscription_id = "/subscriptions/${var.subscription_id}"
  amount          = 10

  time_period {
    start_date = "2026-11-01T00:00:00Z"
  }

  notification {
    threshold      = 80
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Actual"
    contact_roles  = ["Owner"]
  }

  notification {
    threshold      = 100
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Actual"
    contact_roles  = ["Owner"]
  }

  notification {
    threshold      = 100
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Forecasted"
    contact_roles  = ["Owner"]
  }
}
