# Azure Deployment Guide

This guide provides step-by-step instructions for deploying WhatIsInMyFridge to Azure with Test and Prod environments using GitHub Actions and Azure Container Apps.

## Architecture Overview

- **Azure Container Apps** (Consumption plan) - Cost-effective, serverless containers with scale-to-zero
- **Azure File Storage** - Persistent storage for `items.json`
- **GitHub Container Registry** - Store Docker images
- **GitHub Actions** - CI/CD pipelines

## Cost Optimization

The setup uses the cheapest possible Azure resources:
- Container Apps with **0.25 vCPU, 0.5GB memory** (minimum)
- **Scale-to-zero** replicas when not in use
- **Standard_LRS** storage (locally redundant)
- Estimated cost: **$15-20/month** for both environments

## Prerequisites

1. **Azure Account** with an active subscription
2. **Azure CLI** installed ([Install guide](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli))
3. **GitHub Repository** with admin access
4. **Docker** installed locally (optional, for testing)

## Step 1: Set Up Azure Infrastructure

### Option A: Automated Setup (Recommended)

```bash
# Login to Azure
az login

# Set your subscription (optional)
export AZURE_SUBSCRIPTION_ID="your-subscription-id"

# Run the setup script
cd deploy
chmod +x azure-setup.sh
./azure-setup.sh

# Configure secrets and connection strings
chmod +x configure-secrets.sh
./configure-secrets.sh test
./configure-secrets.sh prod
```

This creates:
- **Test environment**: `rg-whatismyfridge-test`
- **Prod environment**: `rg-whatismyfridge-prod`
- All necessary resources in both environments

### Option B: Manual Setup

See the detailed manual steps in `azure-setup.sh` to create resources individually.

## Step 2: Configure GitHub Secrets

### 2.1 Create Azure Service Principal

```bash
az ad sp create-for-rbac \
  --name "whatismyfridge-github" \
  --role contributor \
  --scopes /subscriptions/YOUR_SUBSCRIPTION_ID \
  --sdk-auth
```

Copy the entire JSON output.

### 2.2 Add GitHub Secrets

Go to your GitHub repository:
1. **Settings** > **Secrets and variables** > **Actions**
2. Click **New repository secret**
3. Add the following secret:
   - Name: `AZURE_CREDENTIALS`
   - Value: Paste the JSON from the service principal creation

### 2.3 Configure GitHub Environments (Optional)

For additional protection on production deployments:
1. **Settings** > **Environments**
2. Create `test` and `prod` environments
3. For `prod`, add protection rules (e.g., required reviewers)

## Step 3: Configure Container Registry Access

### Option A: Public Packages (Easiest)

Make your GitHub packages public:
1. Go to **GitHub** > **Packages** > your package
2. **Package settings** > **Change visibility** > **Public**

### Option B: Private Packages with Credentials

1. Create a **Personal Access Token** (PAT):
   - GitHub > **Settings** > **Developer settings** > **Personal access tokens** > **Tokens (classic)**
   - Select scopes: `read:packages`
   
2. Add to Azure Container Apps:
```bash
# For each container app
az containerapp registry set \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --server ghcr.io \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_PAT
```

## Step 4: Build and Push Docker Images

### Using GitHub Actions (Recommended)

1. Go to **Actions** tab in GitHub
2. Select **Build and Push Docker Images**
3. Click **Run workflow**
4. Configure options:
   - ✅ Build Frontend
   - ✅ Build Backend
   - Tag: `v1.0.0` or `latest`
5. Click **Run workflow**

The images will be pushed to:
- `ghcr.io/YOUR_USERNAME/whatismyfridge/frontend:latest`
- `ghcr.io/YOUR_USERNAME/whatismyfridge/backend:latest`

### Manual Build (Alternative)

```bash
# Login to GitHub Container Registry
echo $GITHUB_PAT | docker login ghcr.io -u YOUR_USERNAME --password-stdin

# Build and push frontend
cd frontend
docker build -t ghcr.io/YOUR_USERNAME/whatismyfridge/frontend:latest .
docker push ghcr.io/YOUR_USERNAME/whatismyfridge/frontend:latest

# Build and push backend
cd ../backend
docker build -f WhatIsInMyFridge.Api/Dockerfile -t ghcr.io/YOUR_USERNAME/whatismyfridge/backend:latest .
docker push ghcr.io/YOUR_USERNAME/whatismyfridge/backend:latest
```

## Step 5: Deploy to Azure

### Using GitHub Actions (Recommended)

1. Go to **Actions** tab in GitHub
2. Select **Deploy to Azure**
3. Click **Run workflow**
4. Configure options:
   - Environment: `test` or `prod`
   - ✅ Deploy Frontend
   - ✅ Deploy Backend
   - Image tag: `latest` (or your version tag)
5. Click **Run workflow**

### Manual Deployment

```bash
# Login to Azure
az login

# Deploy backend
az containerapp update \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --image ghcr.io/YOUR_USERNAME/whatismyfridge/backend:latest

# Get backend URL
BACKEND_URL=$(az containerapp show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --query properties.configuration.ingress.fqdn \
  --output tsv)

# Deploy frontend with backend URL
az containerapp update \
  --name ca-frontend-test \
  --resource-group rg-whatismyfridge-test \
  --image ghcr.io/YOUR_USERNAME/whatismyfridge/frontend:latest \
  --set-env-vars "VITE_API_BASE=https://${BACKEND_URL}"
```

## Step 6: Verify Deployment

Get your application URLs:

```bash
# Test environment
az containerapp show --name ca-frontend-test --resource-group rg-whatismyfridge-test --query properties.configuration.ingress.fqdn -o tsv
az containerapp show --name ca-backend-test --resource-group rg-whatismyfridge-test --query properties.configuration.ingress.fqdn -o tsv

# Prod environment
az containerapp show --name ca-frontend-prod --resource-group rg-whatismyfridge-prod --query properties.configuration.ingress.fqdn -o tsv
az containerapp show --name ca-backend-prod --resource-group rg-whatismyfridge-prod --query properties.configuration.ingress.fqdn -o tsv
```

## Workflow Capabilities

### Build Images Workflow
- **Trigger**: Manual or on push to `main`
- **Features**:
  - Choose to build frontend, backend, or both
  - Tag images with custom version
  - Automatic caching for faster builds
  - Pushes to GitHub Container Registry

### Deploy to Azure Workflow
- **Trigger**: Manual only
- **Features**:
  - Select environment (test/prod)
  - Choose what to deploy (frontend, backend, or both)
  - Select image tag to deploy
  - Automatic backend URL configuration for frontend

## Common Operations

### Deploy a new version

```bash
# 1. Build images with version tag
# GitHub Actions > Build and Push Docker Images > Run workflow > Tag: v1.2.3

# 2. Deploy to test first
# GitHub Actions > Deploy to Azure > Environment: test > Image tag: v1.2.3

# 3. After testing, deploy to prod
# GitHub Actions > Deploy to Azure > Environment: prod > Image tag: v1.2.3
```

### Update only frontend

```bash
# Build only frontend
# GitHub Actions > Build and Push Docker Images > Uncheck "Build Backend"

# Deploy only frontend
# GitHub Actions > Deploy to Azure > Uncheck "Deploy Backend"
```

### View logs

```bash
# Stream logs
az containerapp logs show \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --follow

# Or view in Azure Portal
```

### Scale configuration

```bash
# Update scaling rules
az containerapp update \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --min-replicas 1 \
  --max-replicas 5
```

## Troubleshooting

### Image pull errors
- Ensure GitHub packages are public OR credentials are configured
- Verify image names match exactly (case-sensitive)

### Environment variables not working
- Check Container App configuration in Azure Portal
- Redeploy with correct environment variables

### Persistent data not saving
- Verify Azure File Share is mounted correctly
- Check storage account keys are valid

### High costs
- Ensure `min-replicas` is set to 0 for scale-to-zero
- Review Log Analytics retention settings
- Check for unexpected traffic or DDoS

## Clean Up

To delete all resources:

```bash
# Delete test environment
az group delete --name rg-whatismyfridge-test --yes --no-wait

# Delete prod environment
az group delete --name rg-whatismyfridge-prod --yes --no-wait
```

## Additional Resources

- [Azure Container Apps Documentation](https://learn.microsoft.com/en-us/azure/container-apps/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Azure Pricing Calculator](https://azure.microsoft.com/en-us/pricing/calculator/)
