$RESOURCE_GROUP  = "karaparty-rg"
$LOCATION        = "eastasia"
$ACR_NAME        = "karapartycr"         # must be globally unique, lowercase, no hyphens
$ENV_NAME        = "karaparty-env"
$APP_NAME        = "karaparty-scraper"

# 1. Azure Container Registry
az acr create `
  --name $ACR_NAME `
  --resource-group $RESOURCE_GROUP `
  --sku Basic `
  --admin-enabled true

# 2. Container Apps environment
az containerapp env create `
  --name $ENV_NAME `
  --resource-group $RESOURCE_GROUP `
  --location $LOCATION

# 3. Container App (placeholder image — real image deployed via GitHub Actions)
az containerapp create `
  --name $APP_NAME `
  --resource-group $RESOURCE_GROUP `
  --environment $ENV_NAME `
  --image mcr.microsoft.com/dotnet/runtime:10.0 `
  --min-replicas 1 `
  --max-replicas 1 `
  --cpu 1 `
  --memory 2Gi

Write-Host "`n=== ACR credentials for GitHub secrets ===" -ForegroundColor Cyan
$ACR_SERVER = az acr show --name $ACR_NAME --query loginServer --output tsv
$ACR_USER   = az acr credential show --name $ACR_NAME --query username --output tsv
$ACR_PASS   = az acr credential show --name $ACR_NAME --query "passwords[0].value" --output tsv

Write-Host "AZURE_ACR_SERVER   = $ACR_SERVER"
Write-Host "AZURE_ACR_USERNAME = $ACR_USER"
Write-Host "AZURE_ACR_PASSWORD = $ACR_PASS"

Write-Host "`n=== Azure credentials for GitHub secret: AZURE_CREDENTIALS ===" -ForegroundColor Cyan
$SUB_ID = az account show --query id --output tsv
az ad sp create-for-rbac `
  --name "karaparty-scraper-deploy" `
  --role contributor `
  --scopes "/subscriptions/$SUB_ID/resourceGroups/$RESOURCE_GROUP" `
  --json-auth
