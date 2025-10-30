# Local Development Setup

This guide covers setting up the application for local development using .NET Aspire with emulators for Cosmos DB and Azure Blob Storage.

> **🍎 Apple Silicon Users (M1/M2/M3):** The Cosmos DB Emulator doesn't support ARM64 architecture. See **[LOCAL_DEVELOPMENT_APPLE_SILICON.md](LOCAL_DEVELOPMENT_APPLE_SILICON.md)** for specific instructions and alternative approaches.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Deno](https://deno.land/) for the frontend
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (required for Azurite blob storage emulator)
- [Azure Cosmos DB Emulator](https://learn.microsoft.com/azure/cosmos-db/local-emulator) (Windows/Linux x64 only)

## Emulator Setup

### Azure Cosmos DB Emulator

#### Option 1: Windows (Direct Install)
Download and install the Azure Cosmos DB Emulator from:
- https://aka.ms/cosmosdb-emulator

#### Option 2: Docker (Linux x64 only)

**⚠️ Note:** This does NOT work on Apple Silicon Macs. See [LOCAL_DEVELOPMENT_APPLE_SILICON.md](LOCAL_DEVELOPMENT_APPLE_SILICON.md) instead.

```bash
docker run -d \
  -p 8081:8081 \
  -p 10250-10255:10250-10255 \
  --name cosmos-emulator \
  -e AZURE_COSMOS_EMULATOR_PARTITION_COUNT=10 \
  -e AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=true \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
```

The emulator will be available at `https://localhost:8081` with the default credentials.

#### Option 3: Use Azure Cosmos DB Free Tier (All Platforms - Recommended for Mac)

Instead of running a local emulator, you can use the actual Azure Cosmos DB free tier which is free forever:

```bash
az cosmosdb create \
  --name whatsinmyfridge-dev \
  --resource-group WhatIsInMyFridge-Dev \
  --enable-free-tier true
```

See [LOCAL_DEVELOPMENT_APPLE_SILICON.md](LOCAL_DEVELOPMENT_APPLE_SILICON.md) for detailed setup instructions.

**Default Connection String:**
```
AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==
```

### Azurite (Azure Storage Emulator)

Azurite is automatically started by .NET Aspire when you run the AppHost project. No manual installation required!

The emulator runs on:
- Blob Service: `http://127.0.0.1:10000`
- Queue Service: `http://127.0.0.1:10001`
- Table Service: `http://127.0.0.1:10002`

**Default Connection String:**
```
DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;
```

## Project Structure

```
WhatIsInMyFridge/
├── backend/
│   ├── WhatIsInMyFridge.Api/           # Main API project
│   ├── WhatIsInMyFridge.AppHost/       # Aspire orchestration (start this!)
│   └── WhatIsInMyFridge.ServiceDefaults/ # Shared Aspire configuration
└── frontend/                            # Svelte frontend
```

## Running the Application

### Option 1: Using .NET Aspire (Recommended)

The .NET Aspire AppHost automatically orchestrates all services and emulators:

```bash
# From the repository root
dotnet run --project backend/WhatIsInMyFridge.AppHost
```

This will:
1. Start the Cosmos DB emulator (if not already running)
2. Start Azurite for blob storage
3. Start the API project
4. Open the Aspire dashboard at `http://localhost:15888` (or similar)

The Aspire dashboard provides:
- Service health monitoring
- Logs from all services
- Metrics and traces
- Resource connection strings

### Option 2: Manual Startup

If you prefer to run services individually:

1. **Ensure emulators are running**
   ```bash
   # Cosmos DB - start Docker container or native emulator
   docker start cosmos-emulator  # if using Docker
   
   # Azurite is started automatically by Aspire
   ```

2. **Start the API**
   ```bash
   cd backend/WhatIsInMyFridge.Api
   dotnet run
   ```
   
   The API will be available at `http://localhost:5000`

3. **Start the frontend**
   ```bash
   cd frontend
   deno task dev
   ```
   
   The UI will be available at `http://localhost:5173`

## Configuration

### API Configuration (appsettings.Development.json)

The API is configured to use Aspire service connections. Connection strings are automatically injected by Aspire from the AppHost configuration.

Key configuration files:
- `backend/WhatIsInMyFridge.Api/appsettings.Development.json` - Development settings
- `backend/WhatIsInMyFridge.AppHost/AppHost.cs` - Aspire resource configuration

### Aspire Configuration

The AppHost configures the following resources:

```csharp
// Cosmos DB with emulator
var cosmosDb = builder.AddAzureCosmosDB("WhatIsInMyFridge")
    .RunAsEmulator(config => config.WithLifetime(ContainerLifetime.Persistent));

// Azure Storage with emulator (Azurite)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(config => config.WithLifetime(ContainerLifetime.Persistent));

var blobs = storage.AddBlobs("blobs");

// API project with service references
builder.AddProject<Projects.WhatIsInMyFridge_Api>("api")
    .WithReference(cosmosDb)
    .WithReference(blobs);
```

## Verifying Setup

1. **Cosmos DB Emulator**
   - Open https://localhost:8081/_explorer/index.html
   - You should see the Data Explorer UI
   - After running the app, you'll see a database named "WhatIsInMyFridge"

2. **Azurite**
   - Check the Aspire dashboard for blob storage status
   - Use Azure Storage Explorer to connect to the emulator
   - Connection string: (see Azurite section above)

3. **API**
   - Visit http://localhost:5000/swagger
   - You should see the Swagger UI with all endpoints
   - Try http://localhost:5000/health - should return `{"status":"ok"}`

4. **Frontend**
   - Visit http://localhost:5173
   - You should see the login/registration screen

## Database Initialization

The application automatically creates the Cosmos DB database and containers on first run:

```csharp
// From Program.cs
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}
```

Containers created:
- Users
- Households
- FoodItems
- Recipes
- RecipeIngredients
- GroceryItems

## Troubleshooting

### Cosmos DB Emulator Issues

**Problem:** Certificate errors when connecting to the emulator

**Solution:** The emulator uses a self-signed certificate. In development, EF Core automatically accepts it. For other tools, you may need to install the certificate manually.

**Problem:** Port 8081 already in use

**Solution:** Stop any other processes using port 8081, or configure the emulator to use a different port.

### Azurite Issues

**Problem:** Blob storage not working

**Solution:** Ensure Docker Desktop is running. Aspire uses Docker to run Azurite.

**Problem:** "Container already exists" errors

**Solution:** The container name is configured in `BlobStorageService.cs`. Check that it matches the Aspire configuration ("blobs").

### API Startup Issues

**Problem:** `AddServiceDefaults()` or `AddCosmosDbContext()` not found

**Solution:** Ensure you've built the entire solution:
```bash
dotnet build
```

**Problem:** Connection refused to Cosmos DB

**Solution:** Verify the emulator is running and accessible at https://localhost:8081

## Development Workflow

1. **Start Aspire AppHost** (once per development session)
   ```bash
   dotnet run --project backend/WhatIsInMyFridge.AppHost
   ```

2. **Make changes** to API or frontend code

3. **API changes:** Hot reload is enabled by default
   ```bash
   # Or use watch mode explicitly
   dotnet watch --project backend/WhatIsInMyFridge.Api run
   ```

4. **Frontend changes:** Vite hot reload is automatic
   - Just save your files and the browser will update

5. **Monitor via Aspire Dashboard**
   - Check logs, traces, and metrics
   - View service health
   - Inspect connection strings

## Data Persistence

### Emulator Data

Both emulators are configured with `Persistent` lifetime, meaning:
- **Cosmos DB:** Data persists between runs (stored in Docker volume or emulator data directory)
- **Azurite:** Blob storage persists between runs (stored in Docker volume)

### Resetting Data

To start fresh:

**Cosmos DB:**
```bash
# Docker version
docker rm -f cosmos-emulator
docker volume rm cosmos-data  # if you created a named volume

# Then restart the emulator
```

**Azurite:**
```bash
# Data is stored in Docker volumes
# Find and remove the Azurite volume through Docker Desktop or:
docker volume ls | grep azurite
docker volume rm <volume-name>
```

## Production Deployment

When deploying to production:
1. Replace emulator connection strings with Azure Cosmos DB and Azure Storage Account
2. Update `appsettings.Production.json` or use environment variables
3. Ensure proper authentication (Azure AD, Managed Identity, etc.)

See [DEPLOYMENT.md](DEPLOYMENT.md) for detailed deployment instructions.

## Additional Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Cosmos DB Emulator Documentation](https://learn.microsoft.com/azure/cosmos-db/local-emulator)
- [Azurite Documentation](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
- [Entity Framework Core with Cosmos DB](https://learn.microsoft.com/ef/core/providers/cosmos/)
