# Configuration Guide - Connection Strings and Environment Variables

This guide explains how to configure connection strings, CORS, and environment variables for your Azure deployment.

## Overview

The application requires:
- **Backend**: Cosmos DB connection, Blob Storage connection, JWT secret, CORS origins
- **Frontend**: API base URL

## 1. Setting Up Azure Services

### Create Cosmos DB Account

```bash
# Variables
RG="rg-whatismyfridge-test"  # or prod
COSMOS_ACCOUNT="cosmos-whatismyfridge-test"
LOCATION="eastus"

# Create Cosmos DB account (serverless for cost optimization)
az cosmosdb create \
  --name "${COSMOS_ACCOUNT}" \
  --resource-group "${RG}" \
  --locations regionName="${LOCATION}" \
  --kind GlobalDocumentDB \
  --capabilities EnableServerless \
  --default-consistency-level Session

# Create database
az cosmosdb sql database create \
  --account-name "${COSMOS_ACCOUNT}" \
  --resource-group "${RG}" \
  --name "WhatIsInMyFridge"

# Create containers
az cosmosdb sql container create \
  --account-name "${COSMOS_ACCOUNT}" \
  --resource-group "${RG}" \
  --database-name "WhatIsInMyFridge" \
  --name "Users" \
  --partition-key-path "/id"

az cosmosdb sql container create \
  --account-name "${COSMOS_ACCOUNT}" \
  --resource-group "${RG}" \
  --database-name "WhatIsInMyFridge" \
  --name "Households" \
  --partition-key-path "/id"

az cosmosdb sql container create \
  --account-name "${COSMOS_ACCOUNT}" \
  --resource-group "${RG}" \
  --database-name "WhatIsInMyFridge" \
  --name "FoodItems" \
  --partition-key-path "/householdId"

az cosmosdb sql container create \
  --account-name "${COSMOS_ACCOUNT}" \
  --resource-group "${RG}" \
  --database-name "WhatIsInMyFridge" \
  --name "Recipes" \
  --partition-key-path "/householdId"

az cosmosdb sql container create \
  --account-name "${COSMOS_ACCOUNT}" \
  --resource-group "${RG}" \
  --database-name "WhatIsInMyFridge" \
  --name "GroceryItems" \
  --partition-key-path "/householdId"

# Get connection string
COSMOS_CONNECTION_STRING=$(az cosmosdb keys list \
  --name "${COSMOS_ACCOUNT}" \
  --resource-group "${RG}" \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" \
  --output tsv)

echo "Cosmos DB Connection String: ${COSMOS_CONNECTION_STRING}"
```

### Create Blob Storage Account

```bash
# Variables
STORAGE_ACCOUNT="sawhatismyfridgetest"  # must be globally unique, lowercase, no hyphens
RG="rg-whatismyfridge-test"

# Create storage account (if not already created by setup script)
az storage account create \
  --name "${STORAGE_ACCOUNT}" \
  --resource-group "${RG}" \
  --location "${LOCATION}" \
  --sku Standard_LRS

# Create container for photos
az storage container create \
  --name "recipe-photos" \
  --account-name "${STORAGE_ACCOUNT}" \
  --public-access off

# Get connection string
BLOB_CONNECTION_STRING=$(az storage account show-connection-string \
  --name "${STORAGE_ACCOUNT}" \
  --resource-group "${RG}" \
  --query connectionString \
  --output tsv)

echo "Blob Storage Connection String: ${BLOB_CONNECTION_STRING}"
```

## 2. Configure Backend Secrets

### Set Secrets in Container App

```bash
# For Test Environment
az containerapp secret set \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --secrets \
    cosmos-connection-string="YOUR_COSMOS_CONNECTION_STRING" \
    blob-storage-connection-string="YOUR_BLOB_STORAGE_CONNECTION_STRING" \
    jwt-secret-key="$(openssl rand -base64 64 | tr -d '\n')"

# For Prod Environment
az containerapp secret set \
  --name ca-backend-prod \
  --resource-group rg-whatismyfridge-prod \
  --secrets \
    cosmos-connection-string="YOUR_COSMOS_CONNECTION_STRING" \
    blob-storage-connection-string="YOUR_BLOB_STORAGE_CONNECTION_STRING" \
    jwt-secret-key="$(openssl rand -base64 64 | tr -d '\n')"
```

### Update Environment Variables to Use Secrets

```bash
# Get frontend URL for CORS
FRONTEND_URL=$(az containerapp show \
  --name ca-frontend-test \
  --resource-group rg-whatismyfridge-test \
  --query properties.configuration.ingress.fqdn \
  --output tsv)

# Update backend environment variables
az containerapp update \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --set-env-vars \
    ASPNETCORE_ENVIRONMENT=Development \
    COSMOS_CONNECTION_STRING=secretref:cosmos-connection-string \
    BLOB_STORAGE_CONNECTION_STRING=secretref:blob-storage-connection-string \
    JWT_SECRET_KEY=secretref:jwt-secret-key \
    "ALLOWED_ORIGINS=https://${FRONTEND_URL}"
```

## 3. Configure CORS

CORS is automatically configured based on the `ALLOWED_ORIGINS` environment variable in the backend.

### Update CORS Origins

```bash
# Add multiple origins (comma-separated)
az containerapp update \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --set-env-vars \
    "ALLOWED_ORIGINS=https://your-frontend.azurecontainerapps.io,https://custom-domain.com"
```

The backend already includes:
- `http://localhost:5173` (development)
- `https://fridge.colen.at` (your custom domain)
- `https://colen.at`
- Plus any origins from `ALLOWED_ORIGINS` environment variable

## 4. Configure Frontend API URL

The frontend automatically gets the API URL from the deployment workflow, but you can update it manually:

```bash
# Get backend URL
BACKEND_URL=$(az containerapp show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --query properties.configuration.ingress.fqdn \
  --output tsv)

# Update frontend
az containerapp update \
  --name ca-frontend-test \
  --resource-group rg-whatismyfridge-test \
  --set-env-vars "VITE_API_BASE=https://${BACKEND_URL}"
```

## 5. Verify Configuration

### Check Backend Secrets

```bash
# List secrets (won't show values)
az containerapp secret list \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --query "[].name" -o table
```

### Check Environment Variables

```bash
# Backend
az containerapp show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --query "properties.template.containers[0].env" -o table

# Frontend
az containerapp show \
  --name ca-frontend-test \
  --resource-group rg-whatismyfridge-test \
  --query "properties.template.containers[0].env" -o table
```

### Test Endpoints

```bash
# Get URLs
BACKEND_URL=$(az containerapp show --name ca-backend-test --resource-group rg-whatismyfridge-test --query properties.configuration.ingress.fqdn -o tsv)
FRONTEND_URL=$(az containerapp show --name ca-frontend-test --resource-group rg-whatismyfridge-test --query properties.configuration.ingress.fqdn -o tsv)

# Test backend health
curl "https://${BACKEND_URL}/health"

# Test frontend
curl -I "https://${FRONTEND_URL}"
```

## 6. GitHub Actions Configuration

The deployment workflow automatically handles environment variables, but you can customize them in the workflow file.

### Required GitHub Secrets

Add to GitHub repository secrets (Settings > Secrets and variables > Actions):

| Secret Name | Description | How to Get |
|-------------|-------------|------------|
| `AZURE_CREDENTIALS` | Azure service principal | `az ad sp create-for-rbac --name "whatismyfridge-github" --role contributor --scopes /subscriptions/YOUR_SUBSCRIPTION_ID --sdk-auth` |

### Optional: Environment-Specific Secrets

You can also add environment-specific secrets in GitHub Environments:

1. Go to Settings > Environments
2. Create `test` and `prod` environments
3. Add secrets specific to each environment:
   - `COSMOS_CONNECTION_STRING`
   - `BLOB_STORAGE_CONNECTION_STRING`
   - `JWT_SECRET_KEY`

Then update the workflow to use them:

```yaml
- name: Deploy Backend to Azure Container Apps
  run: |
    az containerapp secret set \
      --name ${{ env.BACKEND_APP }} \
      --resource-group ${{ env.RESOURCE_GROUP }} \
      --secrets \
        cosmos-connection-string="${{ secrets.COSMOS_CONNECTION_STRING }}" \
        blob-storage-connection-string="${{ secrets.BLOB_STORAGE_CONNECTION_STRING }}" \
        jwt-secret-key="${{ secrets.JWT_SECRET_KEY }}"
```

## 7. Security Best Practices

### Rotate Secrets Regularly

```bash
# Generate new JWT secret
NEW_JWT_SECRET=$(openssl rand -base64 64 | tr -d '\n')

# Update secret
az containerapp secret set \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --secrets jwt-secret-key="${NEW_JWT_SECRET}"
```

### Use Key Vault (Optional, for Production)

For production, consider using Azure Key Vault:

```bash
# Create Key Vault
az keyvault create \
  --name "kv-whatismyfridge-prod" \
  --resource-group rg-whatismyfridge-prod \
  --location eastus

# Add secrets to Key Vault
az keyvault secret set --vault-name "kv-whatismyfridge-prod" \
  --name "CosmosConnectionString" \
  --value "YOUR_CONNECTION_STRING"

# Grant Container App access to Key Vault
# (requires managed identity setup)
```

## 8. Troubleshooting

### CORS Errors

```bash
# Check current CORS configuration
az containerapp show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --query "properties.template.containers[0].env[?name=='ALLOWED_ORIGINS'].value" -o tsv

# Update if needed
FRONTEND_URL=$(az containerapp show --name ca-frontend-test --resource-group rg-whatismyfridge-test --query properties.configuration.ingress.fqdn -o tsv)
az containerapp update \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --set-env-vars "ALLOWED_ORIGINS=https://${FRONTEND_URL}"
```

### Connection String Issues

```bash
# Verify secrets exist
az containerapp secret list \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test

# Check logs for connection errors
az containerapp logs show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --tail 100
```

### Frontend Can't Reach Backend

```bash
# Verify API base URL in frontend
az containerapp show \
  --name ca-frontend-test \
  --resource-group rg-whatismyfridge-test \
  --query "properties.template.containers[0].env[?name=='VITE_API_BASE'].value" -o tsv

# Should match backend URL
az containerapp show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --query properties.configuration.ingress.fqdn -o tsv
```

## Complete Setup Script

Here's a complete script to set up everything:

```bash
#!/bin/bash
ENV="test"  # or "prod"
RG="rg-whatismyfridge-${ENV}"
BACKEND_APP="ca-backend-${ENV}"
FRONTEND_APP="ca-frontend-${ENV}"
COSMOS_ACCOUNT="cosmos-whatismyfridge-${ENV}"
STORAGE_ACCOUNT="sawhatismyfridge${ENV}"

# 1. Create Cosmos DB (if not exists)
echo "Creating Cosmos DB..."
az cosmosdb create --name "${COSMOS_ACCOUNT}" --resource-group "${RG}" --capabilities EnableServerless --default-consistency-level Session
az cosmosdb sql database create --account-name "${COSMOS_ACCOUNT}" --resource-group "${RG}" --name "WhatIsInMyFridge"

# Create containers
for container in Users Households FoodItems Recipes GroceryItems; do
  az cosmosdb sql container create \
    --account-name "${COSMOS_ACCOUNT}" \
    --resource-group "${RG}" \
    --database-name "WhatIsInMyFridge" \
    --name "${container}" \
    --partition-key-path "/$([ $container = 'Users' ] || [ $container = 'Households' ] && echo 'id' || echo 'householdId')"
done

# 2. Create Blob Storage container
echo "Creating Blob Storage container..."
az storage container create --name "recipe-photos" --account-name "${STORAGE_ACCOUNT}"

# 3. Get connection strings
COSMOS_CONN=$(az cosmosdb keys list --name "${COSMOS_ACCOUNT}" --resource-group "${RG}" --type connection-strings --query "connectionStrings[0].connectionString" -o tsv)
BLOB_CONN=$(az storage account show-connection-string --name "${STORAGE_ACCOUNT}" --resource-group "${RG}" --query connectionString -o tsv)
JWT_SECRET=$(openssl rand -base64 64 | tr -d '\n')

# 4. Set backend secrets
echo "Setting backend secrets..."
az containerapp secret set \
  --name "${BACKEND_APP}" \
  --resource-group "${RG}" \
  --secrets \
    cosmos-connection-string="${COSMOS_CONN}" \
    blob-storage-connection-string="${BLOB_CONN}" \
    jwt-secret-key="${JWT_SECRET}"

# 5. Configure environment variables
FRONTEND_URL=$(az containerapp show --name "${FRONTEND_APP}" --resource-group "${RG}" --query properties.configuration.ingress.fqdn -o tsv)

az containerapp update \
  --name "${BACKEND_APP}" \
  --resource-group "${RG}" \
  --set-env-vars \
    ASPNETCORE_ENVIRONMENT="$([ $ENV = 'prod' ] && echo 'Production' || echo 'Development')" \
    COSMOS_CONNECTION_STRING=secretref:cosmos-connection-string \
    BLOB_STORAGE_CONNECTION_STRING=secretref:blob-storage-connection-string \
    JWT_SECRET_KEY=secretref:jwt-secret-key \
    "ALLOWED_ORIGINS=https://${FRONTEND_URL}"

# 6. Configure frontend
BACKEND_URL=$(az containerapp show --name "${BACKEND_APP}" --resource-group "${RG}" --query properties.configuration.ingress.fqdn -o tsv)

az containerapp update \
  --name "${FRONTEND_APP}" \
  --resource-group "${RG}" \
  --set-env-vars "VITE_API_BASE=https://${BACKEND_URL}"

echo "Configuration complete!"
echo "Frontend: https://${FRONTEND_URL}"
echo "Backend: https://${BACKEND_URL}"
```

Save this as `deploy/configure-secrets.sh` and run it after the initial infrastructure setup.
