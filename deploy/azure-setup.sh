#!/usr/bin/env bash
# One-time Azure setup for SignalSafe. Run it in Azure Cloud Shell (Bash): see docs/DEPLOYMENT.md.
#
# It creates, in one resource group:
#   - an Azure Container Apps environment and app for the API (scales to zero when idle)
#   - an Azure Static Web App (Free plan) for the web app
#   - a service principal that GitHub Actions uses to deploy new API versions
# and prints the values to add to GitHub. Safe to run again: existing resources are kept.

set -euo pipefail

RESOURCE_GROUP="${RESOURCE_GROUP:-signalsafe-rg}"
LOCATION="${LOCATION:-uksouth}"          # where the API runs
# Static Web Apps is offered in a few regions only, and some may not accept new free-trial customers.
# The script tries them in this order. The page itself is served worldwide, so the region hardly matters.
WEB_LOCATIONS="${WEB_LOCATIONS:-eastus2 centralus westus2 eastasia westeurope}"
ENVIRONMENT="signalsafe-env"
API_APP="signalsafe-api"
WEB_APP="signalsafe-web"
IMAGE="${IMAGE:-ghcr.io/velam-varsha/signalsafe-api:latest}"

step() { printf '\n\033[1m==> %s\033[0m\n' "$1"; }

step "Checking you are signed in"
SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
echo "Subscription: $(az account show --query name -o tsv) ($SUBSCRIPTION_ID)"

az config set extension.use_dynamic_install=yes_without_prompt --only-show-errors -o none

step "Registering the Azure services SignalSafe uses (first time can take a few minutes)"
for ns in Microsoft.App Microsoft.Web; do
  az provider register --namespace "$ns" --wait -o none
done

step "Resource group $RESOURCE_GROUP in $LOCATION"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" -o none

step "Container Apps environment $ENVIRONMENT"
if ! az containerapp env show -n "$ENVIRONMENT" -g "$RESOURCE_GROUP" -o none 2>/dev/null; then
  # No log workspace, to stay inside the free allowance.
  az containerapp env create -n "$ENVIRONMENT" -g "$RESOURCE_GROUP" -l "$LOCATION" --logs-destination none -o none
fi

step "API container app $API_APP (image $IMAGE)"
if ! az containerapp show -n "$API_APP" -g "$RESOURCE_GROUP" -o none 2>/dev/null; then
  az containerapp create -n "$API_APP" -g "$RESOURCE_GROUP" \
    --environment "$ENVIRONMENT" \
    --image "$IMAGE" \
    --target-port 8080 --ingress external \
    --min-replicas 0 --max-replicas 1 \
    --cpu 0.25 --memory 0.5Gi \
    -o none
fi
API_URL="https://$(az containerapp show -n "$API_APP" -g "$RESOURCE_GROUP" --query properties.configuration.ingress.fqdn -o tsv)"

step "Static Web App $WEB_APP (Free plan)"
if ! az staticwebapp show -n "$WEB_APP" -g "$RESOURCE_GROUP" -o none 2>/dev/null; then
  created=""
  for region in $WEB_LOCATIONS; do
    echo "Trying region $region..."
    if az staticwebapp create -n "$WEB_APP" -g "$RESOURCE_GROUP" -l "$region" --sku Free -o none --only-show-errors; then
      created="$region"
      break
    fi
    echo "Region $region is not available for this account, trying the next one."
  done
  if [ -z "$created" ]; then
    echo "No Static Web Apps region accepted the request. See docs/DEPLOYMENT.md (Troubleshooting)." >&2
    exit 1
  fi
  echo "Created in $created."
fi
WEB_URL="https://$(az staticwebapp show -n "$WEB_APP" -g "$RESOURCE_GROUP" --query defaultHostname -o tsv)"
WEB_TOKEN="$(az staticwebapp secrets list -n "$WEB_APP" -g "$RESOURCE_GROUP" --query properties.apiKey -o tsv)"

step "Allowing the web app to call the API (CORS)"
az containerapp update -n "$API_APP" -g "$RESOURCE_GROUP" \
  --set-env-vars "Cors__AllowedOrigins__0=$WEB_URL" -o none

step "Deployment identity for GitHub Actions (limited to $RESOURCE_GROUP)"
CREDENTIALS="$(az ad sp create-for-rbac \
  --name "signalsafe-github-deploy" \
  --role contributor \
  --scopes "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP" \
  --json-auth --only-show-errors -o json)"

cat <<EOF

========================================================================
 Done. SignalSafe is set up in Azure.

 API:      $API_URL/swagger
 Web app:  $WEB_URL   (shows a placeholder until the first deploy)

 Now add these in GitHub: your repository > Settings >
 Secrets and variables > Actions. Keep the secrets private:
 never paste them into chat, email or code.

 --- Secrets tab: "New repository secret" ---

 Name:  AZURE_CREDENTIALS
 Value: (everything between the lines below, including the { and })
------------------------------------------------------------------------
$CREDENTIALS
------------------------------------------------------------------------

 Name:  AZURE_STATIC_WEB_APPS_API_TOKEN
 Value: $WEB_TOKEN

 --- Variables tab: "New repository variable" ---

 Name:  API_URL          Value: $API_URL
 Name:  AZURE_DEPLOY     Value: true

 Then: Actions > Deploy > Run workflow.
========================================================================
EOF
