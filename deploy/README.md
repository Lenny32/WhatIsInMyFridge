# Azure Deployment Guide

This guide explains how to deploy the WhatIsInMyFridge application to Azure using GitHub Actions and Azure Bicep.

## Architecture Overview

- **Frontend**: Azure Container Apps (Svelte + Nginx)
- **Backend**: Azure Container Apps (ASP.NET Core)
- **Database**: Azure Cosmos DB (Serverless)
- **Storage**: Azure Blob Storage (for recipe photos)
- **Monitoring**: Application Insights + Log Analytics
- **Region**: Austria East (austriaeast)

## Environments

Two environments are supported:
- **test**: Development/staging environment (scales to 0 when idle)
- **prod**: Production environment (minimum 1 replica)

## Prerequisites

1. **Azure Subscription**: You need two Azure subscriptions (one for test, one for prod)
2. **GitHub Repository**: Fork or clone this repository
3. **GitHub Container Registry**: Images are stored in GitHub Container Registry (GHCR)

## Setup Instructions

### Step 1: Configure Azure Service Principals

Create service principals for each environment:

```bash
# For TEST environment
az ad sp create-for-rbac \
  --name "sp-whatismyfridge-test" \
  --role contributor \
  --scopes /subscriptions/YOUR_TEST_SUBSCRIPTION_ID \
  --sdk-auth

# For PROD environment
az ad sp create-for-rbac \
  --name "sp-whatismyfridge-prod" \
  --role contributor \
  --scopes /subscriptions/YOUR_PROD_SUBSCRIPTION_ID \
  --sdk-auth
```

Save the JSON output for each command.

### Step 2: Configure GitHub Secrets

Add the following secrets to your GitHub repository:

#### Repository Secrets (Settings → Secrets and variables → Actions → Repository secrets)

| Secret Name | Description | Example/Note |
|-------------|-------------|--------------|
| `JWT_SECRET_KEY` | Secret key for JWT token signing | Generate a strong random string (min 32 chars) |

#### Environment Secrets (Settings → Environments → test/prod → Add secret)

Create two environments in GitHub: `test` and `prod`

**For `test` environment:**
| Secret Name | Value |
|-------------|-------|
| `AZURE_CREDENTIALS` | JSON output from test service principal creation |
| `AZURE_SUBSCRIPTION_ID` | Your test Azure subscription ID |

**For `prod` environment:**
| Secret Name | Value |
|-------------|-------|
| `AZURE_CREDENTIALS` | JSON output from prod service principal creation |
| `AZURE_SUBSCRIPTION_ID` | Your prod Azure subscription ID |

### Step 3: Generate JWT Secret Key

```bash
# Generate a random 64-character hex string
openssl rand -hex 32
```

Use this value for the `JWT_SECRET_KEY` secret.

### Step 4: Deploy Infrastructure

Deploy the infrastructure for each environment using GitHub Actions:

1. Go to **Actions** → **Deploy Infrastructure to Azure**
2. Click **Run workflow**
3. Select environment: `test`
4. Click **Run workflow**
5. Wait for completion
6. Repeat for `prod` environment

This will create:
- Resource Group
- Cosmos DB account and database with containers
- Storage Account with blob container
- Container Apps Environment
- Log Analytics Workspace
- Application Insights
- Frontend and Backend Container Apps

### Step 5: Build and Push Docker Images

Trigger the build workflow:

1. Go to **Actions** → **Build and Push Docker Images**
2. Click **Run workflow**
3. Enter tag (e.g., `v1.0.0` or `latest`)
4. Click **Run workflow**

Alternatively, push to the `main` branch and images will be built automatically.

### Step 6: Deploy Applications

Deploy the applications:

1. Go to **Actions** → **Deploy Application to Azure**
2. Click **Run workflow**
3. Select environment: `test`
4. Enter image tag (e.g., `latest`)
5. Click **Run workflow**

The deployment will output the frontend and backend URLs.

## Continuous Deployment

The repository is configured for automatic deployment:

1. **Push to main branch** → Builds Docker images automatically
2. **After successful build** → Automatically deploys to `test` environment
3. **Manual promotion** → Deploy to `prod` via workflow dispatch

## Configuration Details

### Backend Configuration

The backend requires the following environment variables (configured automatically via Bicep):

- `COSMOS_CONNECTION_STRING`: Cosmos DB connection (from secrets)
- `COSMOS_DATABASE_NAME`: Database name (`WhatIsInMyFridge`)
- `BLOB_STORAGE_CONNECTION_STRING`: Blob storage connection (from secrets)
- `JWT_SECRET_KEY`: JWT signing key (from secrets)
- `ALLOWED_ORIGINS`: Frontend URL for CORS
- `APPLICATIONINSIGHTS_CONNECTION_STRING`: Application Insights connection

### Frontend Configuration

The frontend requires:

- `VITE_API_BASE`: Backend API URL (set at runtime)

The nginx container injects this at startup using the `docker-entrypoint.sh` script.

### CORS Configuration

CORS is configured in two places:

1. **Azure Container Apps**: CORS policy on the backend container app ingress
2. **Backend API**: `ALLOWED_ORIGINS` environment variable in `Program.cs`

Both are automatically set to the frontend URL during deployment.

## Cost Optimization

The infrastructure is optimized for cost:

### Test Environment
- **Container Apps**: Scale to 0 when idle (no cost when not used)
- **Cosmos DB**: Serverless mode (pay per request)
- **Storage**: Standard LRS (lowest cost tier)
- **Log Analytics**: 30-day retention

### Prod Environment
- **Container Apps**: Minimum 1 replica (always on)
- **Cosmos DB**: Serverless mode
- **Storage**: Standard LRS
- **Log Analytics**: 30-day retention

### Estimated Monthly Costs

**Test Environment (minimal usage):**
- Container Apps: ~$0-5/month (mostly idle)
- Cosmos DB: ~$0-10/month (serverless)
- Storage: ~$0-5/month
- Monitoring: ~$0-5/month
- **Total: ~$0-25/month**

**Prod Environment (low-moderate usage):**
- Container Apps: ~$15-30/month
- Cosmos DB: ~$10-30/month
- Storage: ~$5-15/month
- Monitoring: ~$5-15/month
- **Total: ~$35-90/month**

## Monitoring and Logs

### Application Insights

View application metrics and traces:
1. Go to Azure Portal
2. Navigate to the resource group
3. Open Application Insights resource
4. View metrics, logs, and performance data

### Container Logs

View container logs:
```bash
# Backend logs
az containerapp logs show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --follow

# Frontend logs
az containerapp logs show \
  --name ca-frontend-test \
  --resource-group rg-whatismyfridge-test \
  --follow
```

### Log Analytics Queries

Access Log Analytics for advanced queries:
1. Azure Portal → Resource Group → Log Analytics Workspace
2. Click "Logs"
3. Run queries like:

```kusto
// Recent errors
ContainerAppConsoleLogs_CL
| where Level_s == "Error"
| order by TimeGenerated desc
| take 50

// Backend API requests
AppRequests
| where AppRoleName == "ca-backend-test"
| summarize count() by bin(TimeGenerated, 1h)
```

## Troubleshooting

### Check Container App Status

```bash
az containerapp show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --query properties.runningStatus
```

### View Recent Revisions

```bash
az containerapp revision list \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --query "[].{Name:name, Active:properties.active, CreatedTime:properties.createdTime}"
```

### Test Connectivity

```bash
# Test backend health endpoint
BACKEND_URL=$(az containerapp show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --query properties.configuration.ingress.fqdn \
  --output tsv)

curl https://${BACKEND_URL}/health
```

## Clean Up

To delete all resources for an environment:

```bash
# Delete test environment
az group delete --name rg-whatismyfridge-test --yes --no-wait

# Delete prod environment
az group delete --name rg-whatismyfridge-prod --yes --no-wait
```

## Security Considerations

1. **Secrets**: All sensitive data is stored as secrets in Container Apps
2. **HTTPS**: All traffic uses HTTPS (enforced by Container Apps)
3. **CORS**: Restricted to the frontend domain only
4. **Blob Storage**: Private access only (no public blobs)
5. **JWT Tokens**: Secure signing with environment-specific keys
6. **Cosmos DB**: No public access (accessed via connection string)

## Next Steps

1. **Custom Domain**: Add custom domain to Container Apps
2. **SSL Certificates**: Configure custom SSL certificates
3. **Auto-scaling**: Adjust scaling rules based on usage
4. **Backup**: Configure Cosmos DB backup policies
5. **Alerts**: Set up alerts in Application Insights
6. **CDN**: Add Azure CDN for static assets

## Support

For issues with deployment:
1. Check GitHub Actions logs
2. View Azure Portal for resource status
3. Check Application Insights for runtime errors
4. Review container logs via Azure CLI
