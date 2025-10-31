# Food Management System

This repository hosts a food inventory manager that keeps pantry, fridge, and freezer items in sync. The backend is a .NET minimal API using Azure Cosmos DB and Azure Blob Storage (with local emulator support), and the frontend is a Svelte experience powered entirely by Deno tasks—no npm install required.

## Project layout

- `backend/WhatIsInMyFridge.Api/` – ASP.NET Core minimal API with Cosmos DB and Blob Storage.
- `backend/WhatIsInMyFridge.AppHost/` – .NET Aspire orchestration for local development.
- `backend/WhatIsInMyFridge.ServiceDefaults/` – Shared Aspire configuration.
- `frontend/` – Svelte + Vite project driven by `deno.json` tasks.
- `.gitignore` – ignores .NET build folders, dist artifacts, and local env files.

## Getting started

### Quick Start (Using .NET Aspire - Recommended)

1. **Prerequisites**
   - [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
   - [Deno](https://deno.land/)
   - [Docker Desktop](https://www.docker.com/products/docker-desktop/)

2. **Run with Aspire AppHost** (starts all services and emulators)
   ```bash
   dotnet run --project backend/WhatIsInMyFridge.AppHost
   ```
   This automatically:
   - Starts Cosmos DB emulator
   - Starts Azurite (Blob Storage emulator)
   - Starts the API
   - Opens the Aspire dashboard

3. **Start the frontend** (in a separate terminal)
   ```bash
   cd frontend
   deno task dev
   ```
   Visit `http://localhost:5173` to use the application.

For detailed setup instructions, emulator configuration, and troubleshooting, see **[LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md)**.

### Manual Startup (Advanced)

If you need to run services individually:

1. **Ensure emulators are running** (Cosmos DB and Azurite)
2. **Run the API**
   ```bash
   dotnet run --project backend/WhatIsInMyFridge.Api
   ```
3. **Start the frontend**
   ```bash
   cd frontend
   deno task dev
   ```

See [LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md) for complete manual setup instructions.

## API overview

- `GET /api/items` – List items, optionally filtered by `location=fridge|freezer|pantry`.
- `GET /api/items/{id}` – Retrieve a single item.
- `POST /api/items` – Create an item (name, unit, restock threshold, etc.).
- `PATCH /api/items/{id}` – Update quantity, metadata, or shopping list tracking.
- `DELETE /api/items/{id}` – Remove an item.
- `GET /api/items/to-buy` – Items at or below their threshold that should be added to the shopping list.

Data persists to `backend/data/items.json`; swap the `FoodInventoryStore` service for a database-backed implementation when needed.

## Frontend highlights

- Location tabs with inline quantity adjustments.
- Quick-add form covering expiry, notes, and shopping-list flag.
- Shopping list sidebar sourced directly from the `/api/items/to-buy` endpoint.

Run `deno task build` for a production bundle or `deno task check` to type-check and lint the Svelte project.

## Automated API Client Generation

This project supports **automatic TypeScript client generation** from the backend's OpenAPI/Swagger specification. This ensures type safety and keeps the frontend in sync with backend changes.

### Quick Start

```bash
# 1. Start the backend
cd backend && dotnet run --project WhatIsInMyFridge.Api

# 2. Generate TypeScript types (in a new terminal)
cd frontend && deno task generate-api
```

This creates `frontend/src/lib/api-types.ts` with fully typed interfaces for all API endpoints. See [OPENAPI_CODEGEN.md](OPENAPI_CODEGEN.md) for detailed documentation and usage examples.

### Benefits

✅ Full TypeScript autocomplete for all endpoints  
✅ Compile-time validation of API calls  
✅ Automatic sync when backend changes  
✅ Single source of truth for API contracts

## 🚀 Deployment

### Docker (Recommended)

The **easiest way to deploy** - everything (frontend + backend) in a single container!

```bash
# 1. Set environment variables
cp .env.example .env
# Edit .env with your JWT_SECRET_KEY and COSMOS_CONNECTION_STRING

# 2. Run with Docker Compose
docker-compose -f docker-compose.simple.yml up -d

# 3. Access at http://localhost
```

**GitHub Actions automatically builds and publishes the Docker image** to GitHub Container Registry on every push to `main`.

📦 **Quick Deploy Guide:** [QUICK_DEPLOY.md](QUICK_DEPLOY.md)  
🐳 **Full Docker Guide:** [DOCKER_DEPLOYMENT.md](DOCKER_DEPLOYMENT.md)  
☁️ **Azure/Manual Deploy:** [DEPLOYMENT.md](DEPLOYMENT.md)

**Works on:** Azure Container Instances, AWS ECS, Google Cloud Run, Railway, Fly.io, DigitalOcean, and anywhere Docker runs!
