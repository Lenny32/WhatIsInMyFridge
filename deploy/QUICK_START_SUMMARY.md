# Azure Deployment - Quick Start Summary

## What You Now Have

✅ **Complete Azure Container Apps deployment solution** with:
- Separate Docker containers for Frontend (Svelte + nginx) and Backend (.NET 9)
- Test and Prod environments
- GitHub Actions workflows for building and deploying
- Cost-optimized configuration (~$15-20/month for both environments)
- Automatic CORS and connection string management

## Critical Configuration Points

### 🔐 1. Connection Strings (Required)

The backend needs **two connection strings**:

```bash
# After running azure-setup.sh, configure secrets:
cd deploy
chmod +x configure-secrets.sh

# For Test environment
./configure-secrets.sh test

# For Prod environment
./configure-secrets.sh prod
```

This script automatically:
- Creates Cosmos DB (serverless) with all required containers
- Creates Blob Storage container for recipe photos
- Generates JWT secret key
- Configures all secrets in Azure Container Apps
- Sets up CORS with frontend URL
- Configures frontend with backend API URL

### 🌐 2. CORS Configuration (Automatic)

CORS is **automatically configured** by the deployment workflow and configure-secrets.sh script:

**Backend CORS origins include:**
- `http://localhost:5173` (development)
- `https://fridge.colen.at` (your custom domain)
- `https://colen.at` (your custom domain)
- Frontend Container App URL (automatically added)
- Any URLs in `ALLOWED_ORIGINS` environment variable

**To add custom origins:**
```bash
az containerapp update \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --set-env-vars "ALLOWED_ORIGINS=https://frontend-url.com,https://other-domain.com"
```

### 🔗 3. API Base URL (Automatic)

The frontend **automatically receives** the backend API URL during deployment.

**How it works:**
1. Deploy workflow gets backend URL
2. Sets `VITE_API_BASE` environment variable in frontend container
3. Frontend's `docker-entrypoint.sh` injects it at runtime
4. Frontend `api.ts` uses it for all API calls

**Frontend configuration in `src/lib/api.ts`:**
```typescript
const API_BASE = import.meta.env.VITE_API_BASE || "__VITE_API_BASE__" || "";
```

## Deployment Workflow

### First-Time Setup (One-Time)

```bash
# 1. Create infrastructure
cd deploy
chmod +x azure-setup.sh
./azure-setup.sh

# 2. Configure secrets and connection strings
chmod +x configure-secrets.sh
./configure-secrets.sh test
./configure-secrets.sh prod

# 3. Create GitHub service principal
az ad sp create-for-rbac \
  --name "whatismyfridge-github" \
  --role contributor \
  --scopes /subscriptions/YOUR_SUBSCRIPTION_ID \
  --sdk-auth

# 4. Add AZURE_CREDENTIALS to GitHub Secrets
# Copy the JSON output and add it to:
# GitHub > Settings > Secrets and variables > Actions > New repository secret
# Name: AZURE_CREDENTIALS
# Value: <paste JSON>
```

### Regular Deployment

```bash
# 1. Build images (via GitHub Actions)
# GitHub > Actions > "Build and Push Docker Images" > Run workflow
#   - Build Frontend: ✅
#   - Build Backend: ✅
#   - Tag: latest (or v1.0.0)

# 2. Deploy to Test (via GitHub Actions)
# GitHub > Actions > "Deploy to Azure" > Run workflow
#   - Environment: test
#   - Deploy Frontend: ✅
#   - Deploy Backend: ✅
#   - Image tag: latest

# 3. Test the deployment
curl https://ca-frontend-test.REGION.azurecontainerapps.io

# 4. Deploy to Prod (via GitHub Actions)
# Same as step 2, but select "prod" environment
```

## Environment Variables Summary

### Backend Environment Variables

| Variable | Source | Purpose |
|----------|--------|---------|
| `ASPNETCORE_ENVIRONMENT` | Deployment workflow | Development/Production mode |
| `COSMOS_CONNECTION_STRING` | Secret reference | Cosmos DB connection |
| `BLOB_STORAGE_CONNECTION_STRING` | Secret reference | Blob Storage connection |
| `JWT_SECRET_KEY` | Secret reference | JWT token signing |
| `ALLOWED_ORIGINS` | Deployment workflow | CORS configuration |

### Frontend Environment Variables

| Variable | Source | Purpose |
|----------|--------|---------|
| `VITE_API_BASE` | Deployment workflow | Backend API URL |

## Secrets Management

Secrets are stored in **Azure Container Apps Secrets** (not environment variables):

```bash
# List secrets (won't show values)
az containerapp secret list \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test

# Update a secret
az containerapp secret set \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --secrets jwt-secret-key="NEW_SECRET_VALUE"
```

## Testing Your Deployment

```bash
# Get URLs
BACKEND_TEST=$(az containerapp show --name ca-backend-test --resource-group rg-whatismyfridge-test --query properties.configuration.ingress.fqdn -o tsv)
FRONTEND_TEST=$(az containerapp show --name ca-frontend-test --resource-group rg-whatismyfridge-test --query properties.configuration.ingress.fqdn -o tsv)

# Test backend health
curl "https://${BACKEND_TEST}/health"
# Expected: {"status":"ok"}

# Test frontend
curl -I "https://${FRONTEND_TEST}"
# Expected: HTTP/2 200

# Test CORS (from browser console on frontend)
fetch('https://BACKEND_URL/health', { credentials: 'include' })
  .then(r => r.json())
  .then(console.log)
```

## Common Issues & Solutions

### Issue: CORS errors in browser

**Solution:**
```bash
# Check CORS configuration
az containerapp show --name ca-backend-test --resource-group rg-whatismyfridge-test \
  --query "properties.template.containers[0].env[?name=='ALLOWED_ORIGINS'].value" -o tsv

# Update if needed
FRONTEND_URL=$(az containerapp show --name ca-frontend-test --resource-group rg-whatismyfridge-test --query properties.configuration.ingress.fqdn -o tsv)
az containerapp update --name ca-backend-test --resource-group rg-whatismyfridge-test \
  --set-env-vars "ALLOWED_ORIGINS=https://${FRONTEND_URL}"
```

### Issue: Frontend can't reach backend

**Solution:**
```bash
# Verify frontend has correct API URL
az containerapp show --name ca-frontend-test --resource-group rg-whatismyfridge-test \
  --query "properties.template.containers[0].env[?name=='VITE_API_BASE'].value" -o tsv

# Update if needed
BACKEND_URL=$(az containerapp show --name ca-backend-test --resource-group rg-whatismyfridge-test --query properties.configuration.ingress.fqdn -o tsv)
az containerapp update --name ca-frontend-test --resource-group rg-whatismyfridge-test \
  --set-env-vars "VITE_API_BASE=https://${BACKEND_URL}"
```

### Issue: Connection string errors in backend logs

**Solution:**
```bash
# Re-run configuration script
cd deploy
./configure-secrets.sh test

# Or manually update
az containerapp secret set \
  --name ca-backend-test \
  --resource-group rg-whatismyfridge-test \
  --secrets cosmos-connection-string="YOUR_CONNECTION_STRING"
```

## Documentation Files

- 📘 **DEPLOYMENT_SUMMARY.md** - Complete architecture overview
- 📗 **AZURE_CONTAINER_APPS_GUIDE.md** - Step-by-step setup guide
- 📙 **CONFIGURATION_GUIDE.md** - Detailed connection strings and secrets setup
- 📕 **QUICK_COMMANDS.md** - CLI command reference
- 📝 **QUICK_START_SUMMARY.md** - This file

## Cost Breakdown

**Estimated monthly cost for BOTH environments:**
- Azure Container Apps (scale-to-zero): $5-10
- Cosmos DB (serverless): $3-5
- Blob Storage: $0.50-1
- Log Analytics: $2-5
- **Total: ~$15-20/month**

## Next Steps

1. ✅ Run `azure-setup.sh` to create infrastructure
2. ✅ Run `configure-secrets.sh` for test and prod
3. ✅ Set up GitHub secrets
4. ✅ Build and push Docker images via GitHub Actions
5. ✅ Deploy to test environment
6. ✅ Verify everything works
7. ✅ Deploy to prod environment
8. 🎉 Enjoy your deployed application!

For detailed instructions, see **AZURE_CONTAINER_APPS_GUIDE.md**.
