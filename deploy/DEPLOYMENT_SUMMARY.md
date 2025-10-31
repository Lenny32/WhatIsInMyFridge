# Azure Deployment Summary

## What Has Been Created

### GitHub Actions Workflows

1. **`.github/workflows/build-images.yml`**
   - Builds Docker images for frontend and backend
   - Pushes to GitHub Container Registry (ghcr.io)
   - Can be triggered manually or automatically on push to main
   - Options to build frontend only, backend only, or both
   - Supports custom image tags (e.g., v1.0.0, latest)

2. **`.github/workflows/deploy-azure.yml`**
   - Deploys containers to Azure Container Apps
   - Supports Test and Prod environments
   - Can deploy frontend only, backend only, or both
   - Automatically configures backend URL for frontend
   - Manual trigger only for controlled deployments

### Dockerfiles

1. **`backend/WhatIsInMyFridge.Api/Dockerfile`**
   - Multi-stage build for .NET 9.0 API
   - Optimized for size and security
   - Exposes port 8080
   - Creates `/app/data` directory for items.json persistence

2. **`frontend/Dockerfile`** (updated)
   - Two-stage build: Deno build + nginx runtime
   - Includes runtime environment variable injection script
   - Optimized for production with gzip compression
   - Exposes port 80

3. **`frontend/docker-entrypoint.sh`**
   - Injects API base URL at container startup
   - Allows dynamic backend configuration

### Infrastructure Setup

1. **`deploy/azure-setup.sh`**
   - Automated script to provision all Azure resources
   - Creates both Test and Prod environments
   - Sets up:
     - Resource Groups
     - Log Analytics Workspaces
     - Container Apps Environments
     - Storage Accounts with File Shares
     - Frontend and Backend Container Apps
   - Configures scale-to-zero for cost savings

### Documentation

1. **`deploy/AZURE_CONTAINER_APPS_GUIDE.md`**
   - Complete step-by-step deployment guide
   - Prerequisites and setup instructions
   - GitHub secrets configuration
   - Troubleshooting tips

2. **`deploy/QUICK_COMMANDS.md`**
   - Quick reference for common Azure CLI commands
   - Copy-paste ready commands for deployment
   - Monitoring, scaling, and rollback instructions

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         GitHub                               │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Actions Workflows                                     │  │
│  │  • Build & Push Images → GitHub Container Registry    │  │
│  │  • Deploy to Azure Container Apps                     │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                    Azure Subscription                        │
│                                                              │
│  ┌─────────────────────┐      ┌─────────────────────┐      │
│  │  Test Environment   │      │  Prod Environment   │      │
│  │                     │      │                     │      │
│  │  Frontend App       │      │  Frontend App       │      │
│  │  (nginx + Svelte)   │      │  (nginx + Svelte)   │      │
│  │  ↓                  │      │  ↓                  │      │
│  │  Backend App        │      │  Backend App        │      │
│  │  (.NET 9 API)       │      │  (.NET 9 API)       │      │
│  │  ↓                  │      │  ↓                  │      │
│  │  Azure File Share   │      │  Azure File Share   │      │
│  │  (items.json)       │      │  (items.json)       │      │
│  └─────────────────────┘      └─────────────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

## Resource Naming Convention

### Test Environment
- Resource Group: `rg-whatismyfridge-test`
- Log Analytics: `logs-whatismyfridge-test`
- Container Apps Environment: `cae-whatismyfridge-test`
- Backend App: `ca-backend-test`
- Frontend App: `ca-frontend-test`
- Storage Account: `sawhatismyfridgetest`
- File Share: `items-test`

### Prod Environment
- Resource Group: `rg-whatismyfridge-prod`
- Log Analytics: `logs-whatismyfridge-prod`
- Container Apps Environment: `cae-whatismyfridge-prod`
- Backend App: `ca-backend-prod`
- Frontend App: `ca-frontend-prod`
- Storage Account: `sawhatismyfridgeprod`
- File Share: `items-prod`

## Cost Optimization Features

1. **Scale-to-Zero**: Apps automatically scale down to 0 replicas when idle
2. **Minimum Resources**: 0.25 vCPU, 0.5GB memory per app
3. **HTTP Scaling**: Auto-scales based on concurrent requests
4. **Shared Infrastructure**: Container Apps Environment shared between apps
5. **Standard Storage**: Locally redundant storage (cheapest option)

**Estimated Monthly Cost**: $15-20 for both environments with minimal usage

## Deployment Workflow

### First-Time Setup
1. Run `deploy/azure-setup.sh` to create Azure infrastructure
2. Create Azure service principal for GitHub Actions
3. Add `AZURE_CREDENTIALS` secret to GitHub
4. Make GitHub packages public or configure registry authentication

### Regular Deployment Process
1. **Build Images** (GitHub Actions)
   - Trigger: Push to main or manual
   - Builds and pushes to GitHub Container Registry
   
2. **Deploy to Test** (GitHub Actions)
   - Manual trigger
   - Deploy and test new version
   
3. **Deploy to Prod** (GitHub Actions)
   - Manual trigger
   - Deploy stable version after testing

## Key Features

### Flexibility
- ✅ Choose which component to build (FE, BE, or both)
- ✅ Choose which component to deploy (FE, BE, or both)
- ✅ Choose environment (Test or Prod)
- ✅ Choose image tag to deploy

### Cost Control
- ✅ Scale-to-zero when not in use
- ✅ Minimum resource allocation
- ✅ Consumption-based pricing
- ✅ No dedicated infrastructure

### Production Ready
- ✅ Separate Test and Prod environments
- ✅ Persistent storage for data
- ✅ Automatic HTTPS with custom domains
- ✅ Built-in monitoring and logging
- ✅ Zero-downtime deployments with revisions

### Developer Friendly
- ✅ Manual deployment triggers for control
- ✅ Easy rollback to previous versions
- ✅ Real-time log streaming
- ✅ Environment variable management

## Next Steps

1. **Set up Azure infrastructure**:
   ```bash
   cd deploy
   chmod +x azure-setup.sh
   ./azure-setup.sh
   ```

2. **Configure secrets and connection strings**:
   ```bash
   chmod +x configure-secrets.sh
   ./configure-secrets.sh test
   ./configure-secrets.sh prod
   ```

3. **Configure GitHub**:
   - Create service principal
   - Add AZURE_CREDENTIALS secret
   - Configure package visibility

3. **Configure GitHub**:
   - Create service principal
   - Add AZURE_CREDENTIALS secret
   - Configure package visibility

4. **First deployment**:
   - Run "Build and Push Docker Images" workflow
   - Run "Deploy to Azure" workflow for test environment
   - Verify application works
   - Deploy to prod environment

4. **First deployment**:
   - Run "Build and Push Docker Images" workflow
   - Run "Deploy to Azure" workflow for test environment
   - Verify application works
   - Deploy to prod environment

5. **Optional enhancements**:
   - Set up custom domains
   - Configure GitHub environment protection rules
   - Add automated tests in workflow
   - Set up monitoring alerts in Azure

## Support Files

- 📄 `deploy/AZURE_CONTAINER_APPS_GUIDE.md` - Detailed setup guide
- 📄 `deploy/CONFIGURATION_GUIDE.md` - Connection strings and secrets configuration
- 📄 `deploy/QUICK_COMMANDS.md` - CLI command reference
- 🔧 `deploy/azure-setup.sh` - Infrastructure automation script
- 🔧 `deploy/configure-secrets.sh` - Secrets and connection strings setup
- 🐳 `backend/WhatIsInMyFridge.Api/Dockerfile` - Backend container
- 🐳 `frontend/Dockerfile` - Frontend container
- ⚙️ `.github/workflows/build-images.yml` - Build workflow
- ⚙️ `.github/workflows/deploy-azure.yml` - Deploy workflow

## Maintenance

### Update application
1. Make code changes
2. Run build workflow with new tag
3. Deploy to test
4. After testing, deploy same tag to prod

### Monitor costs
- Use Azure Cost Management in portal
- Set up budget alerts
- Review Container Apps scaling metrics

### Update infrastructure
- Modify `azure-setup.sh` for changes
- Apply updates via Azure CLI or portal
- Test in test environment first

## Troubleshooting

See `deploy/AZURE_CONTAINER_APPS_GUIDE.md` and `deploy/QUICK_COMMANDS.md` for:
- Common issues and solutions
- Diagnostic commands
- Log viewing instructions
- Rollback procedures
