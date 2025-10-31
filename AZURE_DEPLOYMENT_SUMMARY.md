# Azure Deployment Summary

## Deployment Date
October 31, 2025

## Azure Subscription
- **Name**: Visual Studio Enterprise Subscription – MPN
- **ID**: 7937d4d4-579f-449b-a751-4e73b8786e78
- **Tenant**: evolit.co.at

## Deployed Resources

### Resource Group
- **Name**: whatsinmyfridge-rg
- **Location**: Austria East (austriaeast)

### Cosmos DB (Free Tier)
- **Account Name**: whatsinmyfridge-cosmos
- **Endpoint**: https://whatsinmyfridge-cosmos.documents.azure.com:443/
- **Database**: WhatIsInMyFridge
- **Containers**: 
  - Users
  - Households
  - FoodItems
  - Recipes
  - RecipeIngredients
  - GroceryItems
- **Throughput**: 400 RU/s per container
- **Free Tier**: Enabled (1000 RU/s + 25GB free forever)

### Storage Account
- **Account Name**: wimfsto6633
- **Type**: Standard_LRS (Locally Redundant Storage)
- **Container**: recipe-photos

### App Service Plan
- **Name**: whatsinmyfridge-plan
- **SKU**: F1 (Free Tier)
- **OS**: Linux

### App Service (Backend API)
- **Name**: whatsinmyfridge-api
- **Runtime**: .NET 9.0 (DOTNETCORE:9.0)
- **URL**: https://whatsinmyfridge-api.azurewebsites.net
- **Environment Variables**:
  - ASPNETCORE_ENVIRONMENT=Production
  - JWT_SECRET_KEY=*** (configured)
  - COSMOS_CONNECTION_STRING=*** (configured)
  - BLOB_STORAGE_CONNECTION_STRING=*** (configured)

### Static Web App (Frontend)
- **Name**: whatsinmyfridge-frontend
- **Location**: West Europe
- **URL**: https://wonderful-dune-047992a03.3.azurestaticapps.net
- **Framework**: Svelte with Vite
- **API Endpoint**: https://whatsinmyfridge-api.azurewebsites.net

## Deployment Status

### ✅ Completed
- [x] Resource Group created
- [x] Cosmos DB account created with free tier
- [x] Cosmos DB database and all containers created
- [x] Storage Account created
- [x] Blob Storage container created
- [x] App Service Plan created (F1 Free)
- [x] App Service created (.NET 9.0)
- [x] Environment variables configured
- [x] Backend API published and deployed

### ✅ All Complete!
- [x] App Service deployed and running
- [x] Frontend deployed to Static Web App
- [x] CORS configured between frontend and backend

### 📝 Optional Next Steps
- [ ] Configure custom domains (e.g., fridge.colen.at)
- [ ] Set up Application Insights for monitoring
- [ ] Configure CI/CD pipelines

## Testing the Deployment

### Test Backend API

Once the app service finishes starting (may take 5-10 minutes on F1 tier), test it:

```powershell
# Test health endpoint
curl https://whatsinmyfridge-api.azurewebsites.net/health

# Expected response:
# {"status": "ok"}
```

### Register a Test User

```powershell
Invoke-RestMethod -Uri "https://whatsinmyfridge-api.azurewebsites.net/api/auth/register" `
  -Method POST `
  -ContentType "application/json" `
  -Body (@{
    email = "test@example.com"
    password = "Test123!"
    name = "Test User"
    householdName = "Test Household"
  } | ConvertTo-Json)
```

## Frontend Deployment

### Option 1: Azure Static Web Apps (Recommended)

1. Via Azure Portal:
   - Go to Azure Portal
   - Create new Static Web App
   - Connect to your GitHub repository
   - Set build configuration:
     - App location: `frontend`
     - Output location: `dist`
     - Build command: Custom (will use deno)

2. Add GitHub secret for Deno:
   - Repository Settings → Secrets → New secret
   - Name: `DENO_INSTALL_DIR`
   - Value: `/home/runner/.deno`

3. Update GitHub Actions workflow to install Deno

### Option 2: Manual Build and Upload

```powershell
cd D:\git\Personal\WhatIsInMyFridge\frontend

# Update environment variable
$env:VITE_API_BASE="https://whatsinmyfridge-api.azurewebsites.net"

# Build
deno task build

# Create Static Web App
az staticwebapp create `
  --name whatsinmyfridge-frontend `
  --resource-group whatsinmyfridge-rg `
  --location austriaeast

# Upload build artifacts (manual via portal or CLI)
```

### Option 3: Serve from App Service

Alternative: Deploy frontend as a separate App Service or serve static files from the same backend API.

## Cost Estimate

Using Azure Free Tier:

| Resource | Tier | Monthly Cost |
|----------|------|--------------|
| Cosmos DB | Free Tier (1000 RU/s, 25GB) | $0.00 |
| App Service | F1 Free | $0.00 |
| Storage Account | Standard LRS | ~$0.02/GB |
| Static Web App | Free | $0.00 |

**Total**: ~$0-2/month (depending on storage usage)

## Monitoring

### View Logs

```powershell
# Stream App Service logs
az webapp log tail `
  --name whatsinmyfridge-api `
  --resource-group whatsinmyfridge-rg

# View Cosmos DB metrics
az cosmosdb show `
  --name whatsinmyfridge-cosmos `
  --resource-group whatsinmyfridge-rg
```

### Azure Portal
- View all resources: https://portal.azure.com
- Navigate to Resource Group: whatsinmyfridge-rg

## Troubleshooting

### API not responding (503 Service Unavailable)
- F1 tier has slow cold starts (5-10 minutes)
- Free tier has 60 CPU minutes/day limit
- Try accessing again after a few minutes

### Check App Service Logs
```powershell
az webapp log download `
  --name whatsinmyfridge-api `
  --resource-group whatsinmyfridge-rg `
  --log-file app-logs.zip
```

### Restart App Service
```powershell
az webapp restart `
  --name whatsinmyfridge-api `
  --resource-group whatsinmyfridge-rg
```

## Cleanup

To delete all resources and stop incurring any costs:

```powershell
az group delete `
  --name whatsinmyfridge-rg `
  --yes --no-wait
```

**Warning**: This permanently deletes all data including Cosmos DB databases and blob storage.

## Next Steps

1. Wait for App Service to finish starting
2. Test the backend API
3. Deploy the frontend
4. (Optional) Configure custom domains
5. (Optional) Set up Application Insights for monitoring
6. (Optional) Configure CI/CD pipelines

## Useful Commands

```powershell
# Resource group location
$RESOURCE_GROUP = "whatsinmyfridge-rg"
$LOCATION = "austriaeast"

# List all resources
az resource list --resource-group $RESOURCE_GROUP -o table

# Get App Service URL
az webapp show `
  --name whatsinmyfridge-api `
  --resource-group $RESOURCE_GROUP `
  --query "defaultHostName" -o tsv

# Get Cosmos DB endpoint
az cosmosdb show `
  --name whatsinmyfridge-cosmos `
  --resource-group $RESOURCE_GROUP `
  --query "documentEndpoint" -o tsv

# Get Storage Account endpoint
az storage account show `
  --name wimfsto6633 `
  --resource-group $RESOURCE_GROUP `
  --query "primaryEndpoints.blob" -o tsv
```
