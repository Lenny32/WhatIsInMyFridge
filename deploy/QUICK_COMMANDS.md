# Quick Reference: Azure Deployment Commands

## Prerequisites
```bash
# Login to Azure
az login

# Set subscription (if you have multiple)
az account set --subscription "YOUR_SUBSCRIPTION_ID"

# Login to GitHub Container Registry
echo $GITHUB_PAT | docker login ghcr.io -u YOUR_USERNAME --password-stdin
```

## Initial Setup (One-time)

```bash
# Run automated setup script
cd deploy
chmod +x azure-setup.sh
./azure-setup.sh
```

## Create Service Principal for GitHub Actions

```bash
az ad sp create-for-rbac \
  --name "whatismyfridge-github" \
  --role contributor \
  --scopes /subscriptions/YOUR_SUBSCRIPTION_ID \
  --sdk-auth
```
→ Add output as `AZURE_CREDENTIALS` secret in GitHub

## Build & Push Images

### Via GitHub Actions
1. Go to **Actions** > **Build and Push Docker Images**
2. Click **Run workflow**
3. Select components and tag
4. Click **Run workflow**

### Manually
```bash
# Frontend
cd frontend
docker build -t ghcr.io/YOUR_USERNAME/whatismyfridge/frontend:TAG .
docker push ghcr.io/YOUR_USERNAME/whatismyfridge/frontend:TAG

# Backend
cd backend
docker build -f WhatIsInMyFridge.Api/Dockerfile -t ghcr.io/YOUR_USERNAME/whatismyfridge/backend:TAG .
docker push ghcr.io/YOUR_USERNAME/whatismyfridge/backend:TAG
```

## Deploy to Azure

### Via GitHub Actions
1. Go to **Actions** > **Deploy to Azure**
2. Click **Run workflow**
3. Select environment, components, and image tag
4. Click **Run workflow**

### Manually - Test Environment
```bash
ENV="test"
RG="rg-whatismyfridge-${ENV}"
TAG="latest"

# Deploy Backend
az containerapp update \
  --name "ca-backend-${ENV}" \
  --resource-group "${RG}" \
  --image "ghcr.io/YOUR_USERNAME/whatismyfridge/backend:${TAG}"

# Get backend URL and deploy Frontend
BACKEND_URL=$(az containerapp show \
  --name "ca-backend-${ENV}" \
  --resource-group "${RG}" \
  --query properties.configuration.ingress.fqdn -o tsv)

az containerapp update \
  --name "ca-frontend-${ENV}" \
  --resource-group "${RG}" \
  --image "ghcr.io/YOUR_USERNAME/whatismyfridge/frontend:${TAG}" \
  --set-env-vars "VITE_API_BASE=https://${BACKEND_URL}"
```

### Manually - Prod Environment
```bash
# Same as test, but change ENV="prod"
ENV="prod"
# ... repeat commands above
```

## Get Application URLs

```bash
# Test
az containerapp show --name ca-frontend-test --resource-group rg-whatismyfridge-test \
  --query properties.configuration.ingress.fqdn -o tsv

# Prod
az containerapp show --name ca-frontend-prod --resource-group rg-whatismyfridge-prod \
  --query properties.configuration.ingress.fqdn -o tsv
```

## View Logs

```bash
# Backend logs (test)
az containerapp logs show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --follow

# Frontend logs (prod)
az containerapp logs show \
  --name ca-frontend-prod \
  --resource-group rg-whatismyfridge-prod \
  --tail 100
```

## Scale Configuration

```bash
# Update min/max replicas
az containerapp update \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --min-replicas 0 \
  --max-replicas 3

# Update CPU/Memory
az containerapp update \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --cpu 0.5 \
  --memory 1Gi
```

## Registry Authentication (if packages are private)

```bash
# For each app
az containerapp registry set \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --server ghcr.io \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT

az containerapp registry set \
  --name ca-frontend-test \
  --resource-group rg-whatismyfridge-test \
  --server ghcr.io \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT
```

## Environment Variables

```bash
# Update backend environment
az containerapp update \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --set-env-vars \
    "ASPNETCORE_ENVIRONMENT=Development" \
    "CustomSetting=value"

# Update frontend API base
az containerapp update \
  --name ca-frontend-test \
  --resource-group rg-whatismyfridge-test \
  --set-env-vars "VITE_API_BASE=https://your-backend-url.azurecontainerapps.io"
```

## Monitoring

```bash
# Get metrics
az monitor metrics list \
  --resource $(az containerapp show --name ca-backend-test --resource-group rg-whatismyfridge-test --query id -o tsv) \
  --metric "Requests"

# List revisions
az containerapp revision list \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --query "[].{Name:name,Active:properties.active,Created:properties.createdTime}" \
  -o table
```

## Rollback

```bash
# List revisions
az containerapp revision list \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test

# Activate previous revision
az containerapp revision activate \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --revision REVISION_NAME
```

## Clean Up

```bash
# Delete test environment
az group delete --name rg-whatismyfridge-test --yes --no-wait

# Delete prod environment
az group delete --name rg-whatismyfridge-prod --yes --no-wait

# Delete service principal
az ad sp delete --id $(az ad sp list --display-name "whatismyfridge-github" --query [0].appId -o tsv)
```

## Costs

```bash
# View costs for resource group
az consumption usage list \
  --start-date 2024-01-01 \
  --end-date 2024-01-31 \
  --query "[?resourceGroup=='rg-whatismyfridge-test']"

# Or use Azure Cost Management in portal
```

## Troubleshooting

```bash
# Check container app status
az containerapp show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --query "properties.{Provisioning:provisioningState,Running:runningStatus,Health:healthState}" -o table

# Check ingress configuration
az containerapp ingress show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test

# Restart container app
az containerapp revision restart \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --revision LATEST_REVISION_NAME
```
