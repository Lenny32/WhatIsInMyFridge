# Local Development on Apple Silicon (M1/M2/M3)

The Azure Cosmos DB Emulator Docker image doesn't support ARM64 architecture yet. Here are your options for local development on Apple Silicon Macs.

---

## ⭐ Option 1: Use Azure Cosmos DB Free Tier (Recommended)

The easiest and most reliable option is to use the actual Azure Cosmos DB free tier for local development.

### Why This Works Well:
- ✅ **Free forever** (1000 RU/s + 25GB storage)
- ✅ Native performance (no emulation)
- ✅ Identical to production environment
- ✅ No Docker/emulator issues
- ✅ Works on any platform

### Setup Steps:

#### 1. Create Free Cosmos DB Instance

```bash
# Login to Azure
az login

# Create resource group (if you don't have one)
az group create \
  --name WhatIsInMyFridge-Dev \
  --location eastus

# Create Cosmos DB account with FREE TIER
az cosmosdb create \
  --name whatsinmyfridge-dev \
  --resource-group WhatIsInMyFridge-Dev \
  --enable-free-tier true \
  --default-consistency-level Session \
  --locations regionName=eastus

# Get connection string
az cosmosdb keys list \
  --name whatsinmyfridge-dev \
  --resource-group WhatIsInMyFridge-Dev \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv
```

#### 2. Update `appsettings.Development.json`

Replace the emulator connection string with your Azure connection string:

```json
{
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://whatsinmyfridge-dev.documents.azure.com:443/;AccountKey=YOUR_KEY_HERE==",
    "DatabaseName": "WhatIsInMyFridge"
  }
}
```

#### 3. Start the Backend

```bash
cd backend/WhatIsInMyFridge.Api
dotnet watch run
```

The database and containers will be created automatically on first run!

---

## Option 2: Run Cosmos DB Emulator via Rosetta (x86 Emulation)

You can run the x86_64 emulator using Docker's platform flag, but it will be **significantly slower**.

### Setup:

```bash
# Make sure Docker Desktop is running
# Enable "Use Rosetta for x86_64/amd64 emulation" in Docker Desktop settings

# Run emulator with platform flag
docker run -d \
  --platform linux/amd64 \
  -p 8081:8081 \
  -p 10250-10255:10250-10255 \
  --name cosmos-emulator \
  -e AZURE_COSMOS_EMULATOR_PARTITION_COUNT=3 \
  -e AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=true \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest

# Wait 2-3 minutes for emulator to start, then check status
curl -k https://localhost:8081/_explorer/index.html
```

### Known Issues:
- ⚠️ Very slow startup (2-5 minutes)
- ⚠️ High CPU usage
- ⚠️ May crash randomly
- ⚠️ Limited to 3 partitions max

Keep `appsettings.Development.json` with the default emulator connection string:
```json
{
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "DatabaseName": "WhatIsInMyFridge"
  }
}
```

---

## Option 3: Alternative - Use In-Memory Provider (Development Only)

For quick testing without any database, you can temporarily switch to EF Core's in-memory provider.

### Setup:

1. **Add package**:
```bash
cd backend/WhatIsInMyFridge.Api
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

2. **Update `Program.cs`**:
```csharp
// Replace the Cosmos DB configuration (around line 36)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    #if DEBUG
    options.UseInMemoryDatabase("WhatIsInMyFridge");
    #else
    var cosmosConnectionString = builder.Configuration["COSMOS_CONNECTION_STRING"] 
        ?? builder.Configuration["CosmosDb:ConnectionString"]
        ?? throw new InvalidOperationException("Cosmos DB connection string not found");
    var databaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "WhatIsInMyFridge";
    options.UseCosmos(cosmosConnectionString, databaseName);
    #endif
});
```

### ⚠️ Limitations:
- Data is lost when app restarts
- Not suitable for testing production scenarios
- No partition key validation
- Some Cosmos DB features won't work

---

## Recommended Approach

For Apple Silicon developers:

### For Active Development:
**Use Option 1** (Azure Cosmos DB Free Tier)
- Best performance
- Most reliable
- Identical to production
- No local setup headaches

### For Testing/CI:
**Use Option 3** (In-Memory Provider)
- Fast test execution
- No external dependencies
- Reset between test runs

### Avoid:
**Option 2** (Rosetta Emulation) unless you have a specific reason to test against the emulator

---

## Verifying Your Setup

Once configured, test your setup:

```bash
# Start backend
cd backend/WhatIsInMyFridge.Api
dotnet watch run

# In another terminal, test the API
curl http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "name": "Test User"
  }'
```

You should see a successful response with a JWT token.

---

## Cleanup

### If using Azure Cosmos DB Free Tier:

To delete your dev instance (⚠️ this deletes all data):
```bash
az cosmosdb delete \
  --name whatsinmyfridge-dev \
  --resource-group WhatIsInMyFridge-Dev \
  --yes

# Optional: delete the resource group
az group delete \
  --name WhatIsInMyFridge-Dev \
  --yes
```

### If using Docker Emulator:

```bash
docker stop cosmos-emulator
docker rm cosmos-emulator
```

---

## Troubleshooting

### "Cannot connect to Docker daemon"
- Start Docker Desktop
- Disable "Resource Saver" mode in Docker Desktop settings
- Ensure Docker is not in resource-constrained mode

### "Connection refused" when connecting to Cosmos DB
- For Azure: Check firewall rules allow your IP
- For Emulator: Wait 2-5 minutes after starting
- For Emulator: Try `curl -k https://localhost:8081/`

### "Database already exists" errors
- The app auto-creates database/containers
- If you see errors, manually delete and recreate via Azure Portal or Data Explorer

### Performance issues with Emulator
- This is expected on ARM64 via Rosetta
- Consider switching to Azure Cosmos DB Free Tier instead
