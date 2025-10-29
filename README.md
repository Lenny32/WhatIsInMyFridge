# Food Management System

This repository hosts a food inventory manager that keeps pantry, fridge, and freezer items in sync. The backend is a .NET minimal API that persists inventory to JSON, and the frontend is a Svelte experience powered entirely by Deno tasks—no npm install required.

## Project layout

- `backend/WhatIsInMyFridge.Api/` – ASP.NET Core minimal API with JSON storage (`backend/data/items.json`).
- `frontend/` – Svelte + Vite project driven by `deno.json` tasks.
- `.gitignore` – ignores .NET build folders, dist artifacts, and local env files.

## Getting started

1. **Run the API**
   ```bash
   dotnet run --project backend/WhatIsInMyFridge.Api
   ```
   The API serves HTTP on `http://localhost:5000` (health check at `/health`).

2. **Start the frontend (Deno)**
   ```bash
   cd frontend
   deno task dev
   ```
   Vite serves the UI on `http://localhost:5173` and proxies `/api` to the backend.

3. **Optional configuration**
   - Copy `frontend/.env.example` to `frontend/.env` and adjust `VITE_API_BASE` if the API runs on another host or port.

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
