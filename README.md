# What Is In My Fridge

Full‑stack app to manage household food inventory, grocery lists, recipes, and meal prep planning.

- Backend: .NET (Minimal API) with Cosmos DB + Blob Storage
- Frontend: Svelte + Vite powered by Deno
- Local developer experience via .NET Aspire AppHost (Cosmos + Azurite emulators)

## Features

- **Food Inventory Management** — Track what's in your fridge, freezer, and pantry
- **Recipe Management** — Store and organize your favorite recipes with photos
- **Grocery Lists** — Create shopping lists and mark items as purchased
- **Meal Prep Planning** — Plan meals in advance with an interactive calendar
  - Drag-and-drop meal scheduling
  - Recipe details and cooking instructions
  - Generate grocery lists from planned meals
  - Mobile-responsive with orientation-based views (Day/Week)
- **Household Sharing** — Multiple users can share the same household inventory

## Repo Structure

- `backend/` — .NET services
  - `WhatIsInMyFridge.Api/` — HTTP API, Swagger, JWT auth
  - `WhatIsInMyFridge.AppHost/` — .NET Aspire AppHost that runs API, Cosmos emulator, Azurite, and the frontend dev server
  - `WhatIsInMyFridge.ServiceDefaults/` — shared service defaults (health, OpenTelemetry)
- `frontend/` — Svelte app (Vite, Deno)
  - `deno.json` — tasks: `dev`, `build`, `preview`, `generate-api`
  - `scripts/generate-api.ts` — generates typed client from backend OpenAPI
- `deploy/` — Bicep templates and scripts for Azure deployment

## Prerequisites

- .NET SDK 9.0.x (see `global.json`)
- Deno (latest stable)
- Optional, for manual local emulation:
  - Azurite (Blob Storage emulator) — Docker or local install
  - Cosmos DB Emulator (Windows) or an Azure Cosmos DB account
- Docker (optional, for container builds)

## Quick Start (All‑in‑One with Aspire)

This starts everything: API on port 5000, Cosmos emulator, Azurite, and the frontend dev server.

```
# From repo root
dotnet run --project backend/WhatIsInMyFridge.AppHost
```

Then open:
- Frontend: http://localhost:5173
- API Swagger: http://localhost:5000/swagger

Notes:
- The AppHost wires CORS and proxies correctly between frontend and API.
- On non‑Windows environments, Cosmos DB emulator support may vary. If the emulator fails, configure a real Cosmos DB connection (see Manual Setup).

## Manual Setup (Run services separately)

If you prefer to run the backend and frontend on their own:

1) Start Storage (Azurite)

- Docker: `docker run --rm -p 10000-10002:10000-10002 mcr.microsoft.com/azure-storage/azurite`
- Set env var so the API can find it:
  - Linux/macOS: `export AZURE_STORAGE_CONNECTION_STRING=UseDevelopmentStorage=true`
  - Windows (PowerShell): `$env:AZURE_STORAGE_CONNECTION_STRING = 'UseDevelopmentStorage=true'`

2) Start Database (Cosmos)

- Windows: install/start the Cosmos DB Emulator, or
- Provide a real Azure Cosmos DB SQL API connection:
  - `COSMOSDB_CONNECTION_STRING` and `COSMOSDB_DATABASE_NAME` env vars

3) Start the Backend API

```
# From repo root
ASPNETCORE_URLS=http://localhost:5000 \
  dotnet run --project backend/WhatIsInMyFridge.Api
```

- Swagger UI: http://localhost:5000/swagger
- If calling from a browser app on another origin, set CORS origins, e.g.:
  - `ALLOWED_ORIGINS=http://localhost:5173`

4) Start the Frontend

```
cd frontend
# Dev server
deno task dev
# Build static assets
deno task build
# Preview built app
deno task preview
```

The dev server proxies `/api` to `http://localhost:5000` (see `frontend/vite.config.ts`).

## Generating Typed API Client

With the backend running, generate TS types from Swagger:

```
cd frontend
# Uses http://localhost:5000/swagger/v1/swagger.json by default
deno task generate-api
# Or specify a URL
deno task generate-api http://localhost:5000/swagger/v1/swagger.json
```

Types are written to `frontend/src/lib/api-types.ts`.

## Environment & Configuration

Backend required configuration (development defaults are provided except where noted):
- Cosmos DB
  - `ConnectionStrings:CosmosDb` or `COSMOSDB_CONNECTION_STRING` (required)
  - `CosmosDb:DatabaseName` or `COSMOSDB_DATABASE_NAME` (required)
  - In dev, `backend/WhatIsInMyFridge.Api/appsettings.Development.json` points to the local emulator by default.
- Blob Storage
  - `ConnectionStrings:BlobStorage` or `AZURE_STORAGE_CONNECTION_STRING` (required). Use `UseDevelopmentStorage=true` with Azurite.
- JWT
  - `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` are set in `appsettings.json` for development.
  - For production, generate a strong key (≥32 bytes). Example: `openssl rand -base64 48`
- CORS
  - `ALLOWED_ORIGINS` (comma‑separated) to restrict origins; if unset, any origin is allowed during development.

## Docker

Backend API image:
```
cd backend/WhatIsInMyFridge.Api
docker build -t whatis-in-my-fridge-api .
```

Frontend image (serves built assets with nginx):
```
cd frontend
docker build -t whatis-in-my-fridge-web .
# At runtime, set VITE_API_BASE to point to your API
# e.g., docker run -e VITE_API_BASE=https://api.example.com -p 8080:80 whatis-in-my-fridge-web
```

## Azure Deployment

This repo includes infra as code and GitHub Actions.
- `azure.yaml` supports Azure Developer CLI (`azd`) with Container Apps hosting.
- `deploy/bicep/` contains Bicep templates.
- See `.github/workflows/` for CI/CD pipelines.

## Troubleshooting

- Frontend can’t reach API in dev: ensure API is on `http://localhost:5000` (or update `frontend/vite.config.ts` proxy target) and CORS allows the frontend origin.
- Backend fails on startup complaining about connection strings: set `AZURE_STORAGE_CONNECTION_STRING` and Cosmos DB connection values, or run via the AppHost (recommended).
- OpenAPI types generation fails: verify Swagger is reachable at `http://localhost:5000/swagger/v1/swagger.json` and the API is running.
