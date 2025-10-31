# Azure Deployment Files

This directory contains deployment scripts and documentation for deploying WhatIsInMyFridge to Azure.

## 🚀 Quick Start - Container Apps Deployment (Recommended)

**For containerized deployment with Test and Prod environments:**

```bash
# 1. Create infrastructure
chmod +x azure-setup.sh configure-secrets.sh
./azure-setup.sh

# 2. Configure secrets and connection strings
./configure-secrets.sh test
./configure-secrets.sh prod

# 3. See QUICK_START_SUMMARY.md for GitHub Actions setup
```

## 📚 Documentation Files

### Container Apps Deployment (NEW - Recommended)

| File | Description |
|------|-------------|
| **QUICK_START_SUMMARY.md** | ⭐ Start here! Quick overview with connection strings, CORS, API URLs |
| **AZURE_CONTAINER_APPS_GUIDE.md** | Complete step-by-step deployment guide |
| **CONFIGURATION_GUIDE.md** | Detailed connection strings and secrets configuration |
| **DEPLOYMENT_SUMMARY.md** | Architecture overview and what was created |
| **QUICK_COMMANDS.md** | CLI command reference for common operations |

### Scripts

| File | Description |
|------|-------------|
| **azure-setup.sh** | Creates all Azure infrastructure (Container Apps, Storage, etc.) |
| **configure-secrets.sh** | Configures Cosmos DB, Blob Storage, and all secrets |

### Bicep Deployment (Legacy)

The Bicep files (`main.bicep`, `main.parameters.json`, `deploy.sh`) provide an alternative deployment method using Azure App Service. See the Bicep section below for details.

## 🎯 Which Deployment Method?

### Container Apps (Recommended) ✨
- ✅ Separate containers for frontend and backend
- ✅ Scale-to-zero (pay only when used)
- ✅ Test and Prod environments
- ✅ GitHub Actions CI/CD
- ✅ ~$15-20/month for both environments
- 📖 **Start with QUICK_START_SUMMARY.md**

### Bicep/App Service (Legacy)
- ✅ Simpler architecture
- ✅ Free tier available (with limits)
- ✅ Good for prototyping
- ❌ Less scalable
- 📖 **See Bicep section below**

---

## Container Apps Deployment (Recommended)

### What Gets Created

**Infrastructure:**
- 2 Resource Groups (test, prod)
- 2 Container Apps Environments
- 4 Container Apps (2 frontend, 2 backend)
- 2 Cosmos DB accounts (serverless)
- 2 Storage Accounts
- 2 Log Analytics Workspaces

**CI/CD:**
- GitHub Actions workflows for building images
- GitHub Actions workflows for deployment

### Quick Deployment

```bash
# One-time setup
./azure-setup.sh
./configure-secrets.sh test
./configure-secrets.sh prod

# Deploy via GitHub Actions
# See QUICK_START_SUMMARY.md for details
```

### Key Features

✅ **Connection Strings**: Automatically configured (Cosmos DB, Blob Storage)  
✅ **CORS**: Automatically configured with frontend URL  
✅ **API URLs**: Frontend automatically receives backend URL  
✅ **Secrets**: Stored securely in Azure Container Apps  
✅ **Cost**: ~$15-20/month for both environments

### Documentation

| If you want to... | Read this |
|-------------------|-----------|
| Deploy quickly | QUICK_START_SUMMARY.md |
| Understand architecture | DEPLOYMENT_SUMMARY.md |
| Follow step-by-step | AZURE_CONTAINER_APPS_GUIDE.md |
| Configure secrets | CONFIGURATION_GUIDE.md |
| Find CLI commands | QUICK_COMMANDS.md |

---

## Bicep Deployment (Legacy)

### What Gets Deployed

The Bicep template (`main.bicep`) provisions:

1. **Cosmos DB Account** (Free Tier)
   - Database: `WhatIsInMyFridge`
   - 6 Containers: Users, Households, FoodItems, Recipes, RecipeIngredients, GroceryItems
   - Free tier: 1000 RU/s + 25GB storage (free forever)

2. **Storage Account** (Optional)
   - Blob container: `recipe-photos`
   - Used for storing recipe photos in the cloud

3. **App Service Plan** (F1 Free Tier)
   - Linux-based plan for hosting the .NET API

4. **App Service** (.NET 9 API)
   - Backend API with all environment variables configured
   - CORS enabled for frontend access

5. **Static Web App** (Optional, Free Tier)
   - Svelte frontend with GitHub integration
   - Automatic deployment via GitHub Actions

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- Azure subscription (free tier is sufficient)
- (Optional) GitHub repository for automatic frontend deployment

### Quick Start (Bicep)

### 1. Login to Azure

```bash
az login
```

### 2. Configure Parameters

Edit `main.parameters.json`:

```json
{
  "projectName": {
    "value": "whatsinmyfridge"
  },
  "location": {
    "value": "eastus"
  },
  "jwtSecretKey": {
    "value": "your-secure-jwt-secret-key-here"
  },
  "githubRepoUrl": {
    "value": "https://github.com/yourusername/WhatIsInMyFridge"
  },
  "enableBlobStorage": {
    "value": true
  }
}
```

**Important:** Replace `REPLACE_WITH_YOUR_JWT_SECRET` with a secure random string. You can generate one with:

```bash
openssl rand -base64 32
```

### 3. Run Deployment Script

```bash
cd deploy
./deploy.sh
```

The script will:
- ✓ Check prerequisites (Azure CLI, login status)
- ✓ Optionally generate a JWT secret
- ✓ Create resource group
- ✓ Validate Bicep template
- ✓ Deploy infrastructure (~5-10 minutes)
- ✓ Display deployment outputs

### 4. Deploy Backend API

After infrastructure is deployed, deploy the .NET API:

```bash
cd ../backend/WhatIsInMyFridge.Api
dotnet publish -c Release -o ./publish
cd publish && zip -r ../deploy.zip .
az webapp deploy \
  --resource-group whatsinmyfridge-rg \
  --name whatsinmyfridge-api \
  --src-path ../deploy.zip
```

### 5. Deploy Frontend (if using Static Web App)

If you configured `githubRepoUrl`, the frontend will be automatically deployed via GitHub Actions. Otherwise, build and deploy manually:

```bash
cd frontend
deno task build
# Upload dist/ folder to Azure Static Web Apps via Portal or CLI
```

## Configuration Options

### Parameters

| Parameter | Description | Default | Required |
|-----------|-------------|---------|----------|
| `projectName` | Base name for all resources | `whatsinmyfridge` | Yes |
| `location` | Azure region | `eastus` | Yes |
| `jwtSecretKey` | Secret key for JWT authentication | - | Yes |
| `environmentName` | Environment (Production/Staging/Development) | `Production` | No |
| `apiCustomDomain` | Custom domain for API | `""` | No |
| `frontendCustomDomain` | Custom domain for frontend | `""` | No |
| `githubRepoUrl` | GitHub repository URL | `""` | No |
| `githubBranch` | GitHub branch to deploy | `main` | No |
| `enableBlobStorage` | Enable Azure Blob Storage for photos | `true` | No |

### Resource Naming

Resources are named using the `projectName` parameter:

- Cosmos DB: `{projectName}-cosmos`
- Storage Account: `{projectName}storage`
- App Service Plan: `{projectName}-plan`
- App Service: `{projectName}-api`
- Static Web App: `{projectName}-frontend`

### Custom Domains

To use custom domains (e.g., `api.colen.at`, `fridge.colen.at`):

1. Set parameters in `main.parameters.json`:
   ```json
   {
     "apiCustomDomain": {
       "value": "api.colen.at"
     },
     "frontendCustomDomain": {
       "value": "fridge.colen.at"
     }
   }
   ```

2. After deployment, configure DNS and SSL certificates:
   ```bash
   # API custom domain
   az webapp config hostname add \
     --resource-group whatsinmyfridge-rg \
     --webapp-name whatsinmyfridge-api \
     --hostname api.colen.at
   
   # Frontend custom domain
   az staticwebapp hostname set \
     --name whatsinmyfridge-frontend \
     --hostname fridge.colen.at
   ```

## Manual Deployment (without script)

If you prefer to deploy manually:

```bash
# 1. Create resource group
az group create \
  --name whatsinmyfridge-rg \
  --location eastus

# 2. Validate template
az deployment group validate \
  --resource-group whatsinmyfridge-rg \
  --template-file main.bicep \
  --parameters @main.parameters.json

# 3. Deploy infrastructure
az deployment group create \
  --resource-group whatsinmyfridge-rg \
  --template-file main.bicep \
  --parameters @main.parameters.json

# 4. Get outputs
az deployment group show \
  --resource-group whatsinmyfridge-rg \
  --name main \
  --query properties.outputs
```

## Updating Infrastructure

To update existing infrastructure, simply re-run the deployment:

```bash
./deploy.sh
```

Bicep deployments are idempotent - only changed resources will be updated.

## Cost Estimation

Using free tiers:

- **Cosmos DB**: $0/month (1000 RU/s + 25GB free forever)
- **App Service (F1)**: $0/month (60 minutes/day CPU limit)
- **Static Web App**: $0/month (100GB bandwidth/month)
- **Storage Account**: ~$0.02/month (first 5GB free, then $0.0184/GB)

**Total**: ~$0-2/month (depending on photo storage usage)

## Troubleshooting

### Deployment fails with "Free tier already in use"

Cosmos DB free tier is limited to 1 per Azure subscription. Either:
- Use a different subscription
- Set `enableFreeTier: false` in `main.bicep` (will incur costs)
- Delete existing Cosmos DB free tier account

### App Service won't start

Check logs:
```bash
az webapp log tail \
  --resource-group whatsinmyfridge-rg \
  --name whatsinmyfridge-api
```

Common issues:
- Missing environment variables
- Invalid JWT secret
- Cosmos DB connection string issues

### Static Web App build fails

- Ensure `githubRepoUrl` points to your fork
- Check GitHub Actions logs in your repository
- Verify `buildProperties` in Bicep template match your frontend structure

## Cleaning Up

To delete all resources:

```bash
az group delete \
  --name whatsinmyfridge-rg \
  --yes --no-wait
```

**Warning:** This will permanently delete all data including Cosmos DB databases and blob storage.

## Advanced Configuration

### Using Cosmos DB Emulator for Local Development

The backend is already configured to detect the Cosmos DB Emulator. See parent `DEPLOYMENT.md` for setup instructions.

### Migrating from SQLite to Cosmos DB

See `DEPLOYMENT.md` section "Migrating Existing Data from SQLite to Cosmos DB".

### Monitoring and Alerts

Set up Application Insights:

```bash
az monitor app-insights component create \
  --app whatsinmyfridge-insights \
  --location eastus \
  --resource-group whatsinmyfridge-rg

# Link to App Service
az webapp config appsettings set \
  --resource-group whatsinmyfridge-rg \
  --name whatsinmyfridge-api \
  --settings APPLICATIONINSIGHTS_CONNECTION_STRING="<connection-string>"
```

## Support

For issues related to:
- **Bicep templates**: Check [Azure Bicep documentation](https://docs.microsoft.com/azure/azure-resource-manager/bicep/)
- **Cosmos DB**: See `../DEPLOYMENT.md` and [Cosmos DB docs](https://docs.microsoft.com/azure/cosmos-db/)
- **App configuration**: Check `../backend/WhatIsInMyFridge.Api/Program.cs`
