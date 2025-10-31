# Deployment Guide - WhatIsInMyFridge

This guide covers deploying your food inventory app for **FREE** using Azure services.

---

## 🐳 **RECOMMENDED: Docker Deployment**

**The easiest way to deploy!** Everything (frontend + backend) in a single Docker container.

📦 **See [QUICK_DEPLOY.md](QUICK_DEPLOY.md) for the fastest deployment method.**

GitHub Actions automatically builds and publishes the Docker image on every push. Just:
1. Set environment variables (JWT secret, Cosmos DB connection)
2. Run: `docker-compose -f docker-compose.simple.yml up -d`
3. Done! 🎉

Works on: Azure Container Instances, AWS ECS, Google Cloud Run, Railway, Fly.io, DigitalOcean, and anywhere Docker runs.

**Continue below for manual/custom deployment options.**

---

## Architecture Overview

- **Backend**: .NET 9 API with Cosmos DB (NoSQL) deployed on Azure App Service
- **Frontend**: Svelte app deployed on Azure Static Web Apps
- **Storage**: Azure Blob Storage for recipe photos
- **Database**: Azure Cosmos DB (free tier)
- **Total Cost**: $0/month (within free tier limits)

---

## Deployment Options

### Quick Start: Bicep Infrastructure-as-Code (Recommended)

The fastest way to deploy is using the included Bicep templates in the `deploy/` folder. This provisions all Azure resources in a single command.

```bash
cd deploy
./deploy.sh
```

See **[deploy/README.md](deploy/README.md)** for complete Bicep deployment documentation.

**What you get:**
- ✓ Cosmos DB account with all containers
- ✓ Blob Storage for recipe photos
- ✓ App Service for .NET API
- ✓ Static Web App for Svelte frontend
- ✓ All environment variables configured
- ✓ CORS and security settings
- ✓ Deployment in ~5-10 minutes

**Continue to Option 2 below for manual deployment steps.**

---

## Option 1: Azure Deployment (Manual Setup)

### Prerequisites

1. Azure Account (free tier available)
2. Azure CLI installed
3. Git repository (GitHub recommended)

### Step 1: Create Azure Cosmos DB (Free Tier)

Azure offers 1000 RU/s and 25GB storage completely free forever.

```bash
# Login to Azure
az login

# Create resource group
az group create --name WhatIsInMyFridge-RG --location eastus

# Create Cosmos DB account (free tier)
az cosmosdb create \
  --name whatsinmyfridge \
  --resource-group WhatIsInMyFridge-RG \
  --enable-free-tier true \
  --default-consistency-level Session \
  --locations regionName=eastus

# Get connection string
az cosmosdb keys list \
  --name whatsinmyfridge \
  --resource-group WhatIsInMyFridge-RG \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv
```

Save the connection string - you'll need it later.

### Step 2: Create Azure Blob Storage (Optional)

For recipe photo storage. Can skip this for local development.

```bash
# Create storage account (LRS for lowest cost)
az storage account create \
  --name whatsinmyfridgestorage \
  --resource-group WhatIsInMyFridge-RG \
  --location eastus \
  --sku Standard_LRS

# Get connection string
az storage account show-connection-string \
  --name whatsinmyfridgestorage \
  --resource-group WhatIsInMyFridge-RG \
  --query connectionString -o tsv
```

The app will use local file storage if Blob Storage is not configured.

### Step 3: Deploy Backend to Azure App Service (F1 Free Tier)

```bash
# Create App Service plan (F1 = free tier)
az appservice plan create \
  --name WhatIsInMyFridge-Plan \
  --resource-group WhatIsInMyFridge-RG \
  --sku F1 \
  --is-linux

# Create web app
az webapp create \
  --name whatsinmyfridge-api \
  --resource-group WhatIsInMyFridge-RG \
  --plan WhatIsInMyFridge-Plan \
  --runtime "DOTNET:9.0"

# Configure environment variables
az webapp config appsettings set \
  --name whatsinmyfridge-api \
  --resource-group WhatIsInMyFridge-RG \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    JWT_SECRET_KEY=$(openssl rand -base64 32) \
    COSMOS_CONNECTION_STRING="<your-cosmos-connection-string>" \
    BLOB_STORAGE_CONNECTION_STRING="<your-blob-storage-connection-string>"

# Deploy from local git
cd /Users/lenny/Projects/WhatIsInMyFridge
az webapp deployment source config-local-git \
  --name whatsinmyfridge-api \
  --resource-group WhatIsInMyFridge-RG

# Get deployment URL
az webapp deployment list-publishing-credentials \
  --name whatsinmyfridge-api \
  --resource-group WhatIsInMyFridge-RG

# Add Azure remote and push
git remote add azure <git-deployment-url>
git push azure main
```

Your backend will be available at:
```
https://whatsinmyfridge-api.azurewebsites.net
```

### Step 4: Deploy Frontend to Azure Static Web Apps

Azure Static Web Apps has a generous free tier perfect for this app.

```bash
# Create static web app (connects to GitHub)
az staticwebapp create \
  --name whatsinmyfridge-frontend \
  --resource-group WhatIsInMyFridge-RG \
  --location eastus2 \
  --source https://github.com/<your-username>/WhatIsInMyFridge \
  --branch main \
  --app-location "frontend" \
  --output-location "dist" \
  --build-command "deno task build"

# Set environment variable
az staticwebapp appsettings set \
  --name whatsinmyfridge-frontend \
  --setting-names VITE_API_BASE=https://whatsinmyfridge-api.azurewebsites.net
```

Alternative: Deploy via GitHub Actions (recommended):

1. Go to Azure Portal
2. Create a new Static Web App
3. Connect your GitHub repository
4. Set build configuration:
   - **App location**: `frontend`
   - **Output location**: `dist`
   - **Build command**: `deno task build`

Azure will automatically set up GitHub Actions for CI/CD.

Your frontend will be available at:
```
https://whatsinmyfridge-frontend.azurestaticapps.net
```

### Step 5: Configure Custom Domain (Optional)

For your `colen.at` domain:

#### Backend (App Service):
```bash
# Add custom domain
az webapp config hostname add \
  --webapp-name whatsinmyfridge-api \
  --resource-group WhatIsInMyFridge-RG \
  --hostname api.colen.at

# Enable HTTPS
az webapp update \
  --name whatsinmyfridge-api \
  --resource-group WhatIsInMyFridge-RG \
  --https-only true
```

Add DNS records:
```
CNAME api.colen.at -> whatsinmyfridge-api.azurewebsites.net
```

#### Frontend (Static Web App):
```bash
# Add custom domain
az staticwebapp hostname set \
  --name whatsinmyfridge-frontend \
  --resource-group WhatIsInMyFridge-RG \
  --hostname fridge.colen.at
```

Add DNS records:
```
CNAME fridge.colen.at -> whatsinmyfridge-frontend.azurestaticapps.net
```

### Step 6: Update CORS Settings

After getting your frontend URL, update the backend CORS settings in `Program.cs`:

The CORS configuration is already set up in `Program.cs` (lines 86-100) to allow:
- `http://localhost:5173` (local development)
- `https://fridge.colen.at` (custom domain)
- `https://colen.at`

If you're using a different domain, update these origins in `backend/WhatIsInMyFridge.Api/Program.cs`.

---

## Local Development with Azure Resources

### Option A: Use Azure Cosmos DB Emulator

Download and install the [Azure Cosmos DB Emulator](https://docs.microsoft.com/azure/cosmos-db/local-emulator):

```bash
# Windows
choco install azure-cosmosdb-emulator

# macOS/Linux (via Docker)
docker run -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254 \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```

The emulator connection string is already configured in `appsettings.Development.json`.

### Option B: Use Azure Free Tier Resources

You can point your local development to the free tier Azure resources:

1. Create `.env` file in backend:
```bash
COSMOS_CONNECTION_STRING=<your-azure-cosmos-connection-string>
BLOB_STORAGE_CONNECTION_STRING=<your-azure-blob-storage-connection-string>
```

2. Run locally:
```bash
cd backend/WhatIsInMyFridge.Api
dotnet run
```

---

## Testing Your Deployment

### 1. Test Backend Health
```bash
curl https://whatsinmyfridge-api.azurewebsites.net/health
```

Expected response:
```json
{"status": "ok"}
```

### 2. Test API
```bash
# Register a user
curl -X POST https://whatsinmyfridge-api.azurewebsites.net/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "name": "Test User",
    "householdName": "Test Household"
  }'
```

### 3. Test Frontend
Visit your Static Web App URL and try:
1. Register a new account
2. Add items to your inventory
3. Create a recipe with photos
4. Use the grocery list

### 4. Test Cosmos DB
Check the Azure Portal to verify data is being created in Cosmos DB containers.

---

## Monitoring & Maintenance

### Azure Portal
- Monitor App Service metrics (CPU, memory, requests)
- View Cosmos DB request units and storage
- Check Blob Storage usage
- View logs in Log Stream

### Azure CLI
```bash
# View App Service logs
az webapp log tail \
  --name whatsinmyfridge-api \
  --resource-group WhatIsInMyFridge-RG

# Check Cosmos DB metrics
az cosmosdb show \
  --name whatsinmyfridge \
  --resource-group WhatIsInMyFridge-RG
```

### Application Insights (Optional)
Add Application Insights for detailed telemetry:

```bash
az monitor app-insights component create \
  --app whatsinmyfridge-insights \
  --location eastus \
  --resource-group WhatIsInMyFridge-RG \
  --application-type web

# Get instrumentation key
az monitor app-insights component show \
  --app whatsinmyfridge-insights \
  --resource-group WhatIsInMyFridge-RG \
  --query instrumentationKey -o tsv
```

---

## Costs & Limits

### Azure Free Tier

**Cosmos DB (Free Tier):**
- ✅ 1000 RU/s throughput
- ✅ 25 GB storage
- ✅ Completely free forever (one per subscription)
- ✅ No credit card required for free tier

**App Service (F1 Free):**
- ✅ 1 GB disk space
- ✅ 60 CPU minutes/day
- ✅ 1 GB RAM
- ⚠️ Always-on not available
- ⚠️ Limited to 10 apps

**Static Web Apps (Free):**
- ✅ 100 GB bandwidth/month
- ✅ Unlimited deployments
- ✅ Free SSL certificates
- ✅ Custom domains

**Blob Storage:**
- ⚠️ First 5 GB free for first 12 months
- Pay as you go after ($0.018/GB/month)
- Alternative: Use local file storage (free)

### When You Outgrow Free Tier
- App Service Basic: ~$13/month
- Cosmos DB Standard: ~$25/month (400 RU/s)
- Blob Storage: ~$0.02/GB/month

---

## Alternative Deployment Options (Legacy)

> **Note**: The following deployment options (Railway, Vercel, Fly.io) are provided for reference but are not actively maintained. **Azure with Bicep** (see above) is the recommended deployment method.

---

## Option 2: Railway + Vercel

Railway offers a generous free tier perfect for this app.

#### 1. Sign Up for Railway
- Visit [railway.app](https://railway.app)
- Sign up with GitHub

#### 2. Create New Project
```bash
# Install Railway CLI (optional)
npm i -g @railway/cli

# Login
railway login

# Deploy from project directory
cd /Users/lenny/Projects/WhatIsInMyFridge
railway init
railway up
```

#### 3. Set Environment Variables in Railway Dashboard
Go to your Railway project settings and add:
```
JWT_SECRET_KEY=<generate-a-random-32+-character-string>
ASPNETCORE_ENVIRONMENT=Production
```

Generate a secure JWT key:
```bash
openssl rand -base64 32
```

#### 4. Configure Port
Railway automatically detects the Dockerfile and sets the PORT environment variable. The app is configured to listen on port 8080.

#### 5. Get Your Backend URL
After deployment, Railway will provide a URL like:
```
https://whatsinmyfridge-production.up.railway.app
```

**Note**: Railway's free tier includes:
- 500 hours/month execution time
- 512 MB RAM
- 1 GB disk (perfect for SQLite)
- Automatic HTTPS

---

### Frontend Deployment (Vercel)

Vercel has excellent free tier for static sites.

#### 1. Sign Up for Vercel
- Visit [vercel.com](https://vercel.com)
- Sign up with GitHub

#### 2. Import Your Repository
- Click "New Project"
- Import your GitHub repository
- Vercel will auto-detect it's a Vite project

#### 3. Configure Build Settings
In Vercel project settings:

**Framework Preset**: Vite
**Build Command**: 
```bash
cd frontend && deno task build
```

**Output Directory**: 
```
frontend/dist
```

**Install Command**: 
```bash
curl -fsSL https://deno.land/x/install/install.sh | sh && export PATH="$HOME/.deno/bin:$PATH"
```

#### 4. Set Environment Variable
In Vercel Environment Variables, add:
```
VITE_API_BASE=https://your-railway-backend-url.up.railway.app
```
(Use the URL from Railway step 5)

#### 5. Deploy
Click "Deploy" - Vercel will build and deploy your frontend!

Your frontend will be available at:
```
https://what-is-in-my-fridge.vercel.app
```

---

## Option 3: All-in-One Railway Deployment

You can also deploy both frontend and backend on Railway.

### 1. Backend Service
Follow the Railway backend steps above.

### 2. Frontend Service
- Create a new service in the same Railway project
- Add this `frontend/Dockerfile`:

```dockerfile
FROM denoland/deno:1.40.0

WORKDIR /app

# Copy frontend files
COPY frontend/ .

# Install dependencies and build
RUN deno task build

# Install a simple static server
RUN deno install --allow-net --allow-read https://deno.land/std/http/file_server.ts

# Serve the built files
CMD ["deno", "run", "--allow-net", "--allow-read", "https://deno.land/std/http/file_server.ts", "dist", "--port", "8080"]
```

Set environment variable:
```
VITE_API_BASE=<your-railway-backend-internal-url>
```

---

## Option 4: Fly.io

Fly.io offers a good free tier if you prefer a single platform.

### 1. Install Fly CLI
```bash
curl -L https://fly.io/install.sh | sh
```

### 2. Login and Launch
```bash
flyctl auth login
flyctl launch
```

### 3. Configure
Edit the generated `fly.toml`:
```toml
app = "whatsinmyfridge"

[build]
  dockerfile = "Dockerfile"

[env]
  ASPNETCORE_ENVIRONMENT = "Production"
  ASPNETCORE_URLS = "http://+:8080"

[[services]]
  internal_port = 8080
  protocol = "tcp"

  [[services.ports]]
    port = 80
    handlers = ["http"]

  [[services.ports]]
    port = 443
    handlers = ["tls", "http"]

[mounts]
  source = "whatsinmyfridge_data"
  destination = "/app/data"
```

### 4. Set Secrets
```bash
flyctl secrets set JWT_SECRET_KEY=$(openssl rand -base64 32)
```

### 5. Deploy
```bash
flyctl deploy
```

**Free Tier**: 
- 3 shared-cpu-1x VMs with 256MB RAM
- 160GB outbound data transfer
- Persistent volumes (3GB)

---

---

## Alternative: Railway + Vercel (Legacy SQLite Deployment)

If you prefer SQLite over Cosmos DB, see the [legacy deployment guide](./docs/RAILWAY_DEPLOYMENT.md).

**Note**: The Railway deployment uses SQLite instead of Cosmos DB. The main codebase now uses Cosmos DB by default.

---

## Testing Your Deployment

### 1. Test Backend Health
```bash
curl https://your-backend-url.railway.app/health
```

Expected response:
```json
{"status": "ok"}
```

### 2. Test API
```bash
# Register a user
curl -X POST https://your-backend-url.railway.app/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "name": "Test User",
    "householdName": "Test Household"
  }'
```

### 3. Test Frontend
Visit your Vercel URL and try:
1. Register a new account
2. Add items to your inventory
3. Create a recipe
4. Use the grocery list

---

## Monitoring & Maintenance

### Railway
- Monitor in Railway dashboard
- Check logs: `railway logs`
- View metrics (CPU, memory, network)

### Vercel
- Check deployment logs in dashboard
- Monitor Core Web Vitals
- View function logs if using serverless functions

### Backups (SQLite)
Create a backup script:
```bash
#!/bin/bash
# Download SQLite database from Railway
railway run cat /app/data/whatsinmyfridge.db > backup-$(date +%Y%m%d).db
```

Run weekly via cron or GitHub Actions.

---

## Costs & Limits

### Free Tier Limits

**Railway:**
- ✅ $5 free credit/month
- ✅ 500 execution hours
- ✅ 512MB RAM
- ✅ 1GB disk
- ⚠️ Credit card required after trial

**Vercel:**
- ✅ 100GB bandwidth/month
- ✅ Unlimited deployments
- ✅ No credit card required
- ✅ Custom domains

**Fly.io:**
- ✅ 3 shared VMs (256MB each)
- ✅ 160GB bandwidth/month
- ✅ 3GB persistent storage
- ⚠️ Credit card required

### When You Outgrow Free Tier
- Railway Pro: $5/month + usage
- Vercel Pro: $20/month
- Fly.io: Pay as you go (~$2-5/month for small apps)

---

## Troubleshooting

### Backend won't start
1. Check App Service logs: `az webapp log tail --name whatsinmyfridge-api --resource-group WhatIsInMyFridge-RG`
2. Verify environment variables are set (especially `COSMOS_CONNECTION_STRING`)
3. Ensure JWT_SECRET_KEY is at least 32 characters
4. Check Cosmos DB connection string is valid

### Cosmos DB connection errors
1. Verify connection string is correct
2. Check Cosmos DB firewall settings (allow Azure services)
3. Ensure database name matches configuration (default: "WhatIsInMyFridge")
4. Verify free tier is enabled on your Cosmos DB account

### Frontend can't connect to backend
1. Verify `VITE_API_BASE` environment variable in Static Web App
2. Check CORS settings in `Program.cs` - ensure your frontend URL is allowed
3. Ensure backend URL is HTTPS
4. Check browser console for errors

### Photo upload fails
1. Check if `BLOB_STORAGE_CONNECTION_STRING` is set (optional - will use local storage if not set)
2. Verify Blob Storage container permissions
3. Check file size limits (5MB max)
4. Ensure allowed file types (JPEG, PNG, WebP)

### Database initialization fails
1. Ensure Cosmos DB is created and accessible
2. Check that `EnsureCreatedAsync()` is being called on startup
3. Verify partition key configuration matches your models
4. Check App Service logs for specific error messages

### Add CORS if needed
The CORS configuration is in `Program.cs` (lines 86-100). To add more origins:

```csharp
app.UseCors(options =>
{
    options.WithOrigins(
            "http://localhost:5173",
            "https://fridge.colen.at",
            "https://colen.at",
            "https://your-new-domain.com"  // Add your domain here
          )
          .AllowAnyMethod()
          .AllowAnyHeader()
          .AllowCredentials();
});
```

---

## Backup & Recovery

### Cosmos DB Backup
- Azure automatically backs up Cosmos DB every 4 hours
- Retention: 30 days (free tier) or 7 days (continuous backup)
- Restore via Azure Portal or support ticket

### Export Data
```bash
# Install Azure Cosmos DB Data Migration Tool
# Export to JSON
dt.exe /s:DocumentDB /s.ConnectionString:"<cosmos-connection-string>" \
  /s.Collection:Users /t:JsonFile /t.File:users-backup.json
```

### Photo Backup (Blob Storage)
```bash
# Download all photos
az storage blob download-batch \
  --account-name whatsinmyfridgestorage \
  --source recipe-photos \
  --destination ./photo-backup
```

---

## Next Steps

1. ✅ Create Azure Cosmos DB (free tier)
2. ✅ Create Azure Blob Storage (optional)
3. ✅ Deploy backend to Azure App Service
4. ✅ Deploy frontend to Azure Static Web Apps
5. ✅ Test the full application
6. 📝 Configure custom domain (fridge.colen.at)
7. 📝 Set up Application Insights (optional)
8. 📝 Configure automated backups

---

## Quick Deploy Commands

```bash
# Login to Azure
az login

# Create all resources
RESOURCE_GROUP="WhatIsInMyFridge-RG"
LOCATION="eastus"
COSMOS_NAME="whatsinmyfridge"
STORAGE_NAME="whatsinmyfridgestorage"
APP_NAME="whatsinmyfridge-api"
STATIC_APP_NAME="whatsinmyfridge-frontend"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create Cosmos DB (free tier)
az cosmosdb create \
  --name $COSMOS_NAME \
  --resource-group $RESOURCE_GROUP \
  --enable-free-tier true \
  --locations regionName=$LOCATION

# Create Blob Storage
az storage account create \
  --name $STORAGE_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS

# Get connection strings
COSMOS_CONN=$(az cosmosdb keys list --name $COSMOS_NAME --resource-group $RESOURCE_GROUP --type connection-strings --query "connectionStrings[0].connectionString" -o tsv)
BLOB_CONN=$(az storage account show-connection-string --name $STORAGE_NAME --resource-group $RESOURCE_GROUP --query connectionString -o tsv)

# Create and deploy App Service
az appservice plan create \
  --name WhatIsInMyFridge-Plan \
  --resource-group $RESOURCE_GROUP \
  --sku F1 \
  --is-linux

az webapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan WhatIsInMyFridge-Plan \
  --runtime "DOTNET:9.0"

az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    JWT_SECRET_KEY=$(openssl rand -base64 32) \
    COSMOS_CONNECTION_STRING="$COSMOS_CONN" \
    BLOB_STORAGE_CONNECTION_STRING="$BLOB_CONN"

# Deploy via GitHub Actions (recommended) or local git
az webapp deployment source config-local-git \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP

echo "Backend deployed! Now create Static Web App via Azure Portal and connect to GitHub"
echo "Backend URL: https://$APP_NAME.azurewebsites.net"
```

That's it! Your app should now be live on Azure for free! 🎉

---

## Migration from SQLite to Cosmos DB

If you have an existing SQLite deployment, here's how to migrate:

### 1. Export SQLite Data
```bash
# Install sqlite3
brew install sqlite3  # macOS

# Export users
sqlite3 whatsinmyfridge.db "SELECT * FROM Users" -json > users.json

# Export households
sqlite3 whatsinmyfridge.db "SELECT * FROM Households" -json > households.json

# Export food items
sqlite3 whatsinmyfridge.db "SELECT * FROM FoodItems" -json > fooditems.json

# Export recipes
sqlite3 whatsinmyfridge.db "SELECT * FROM Recipes" -json > recipes.json
```

### 2. Import to Cosmos DB
Use the Azure Cosmos DB Data Migration Tool or write a simple C# script:

```csharp
// Migration script example
var context = new AppDbContext(cosmosOptions);

// Parse JSON and add to Cosmos DB
var users = JsonSerializer.Deserialize<List<User>>(File.ReadAllText("users.json"));
context.Users.AddRange(users);

var households = JsonSerializer.Deserialize<List<Household>>(File.ReadAllText("households.json"));
context.Households.AddRange(households);

await context.SaveChangesAsync();
```

### 3. Verify Migration
Check Azure Portal to ensure all data is imported correctly into Cosmos DB containers.

---