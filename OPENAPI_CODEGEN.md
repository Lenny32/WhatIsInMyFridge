# OpenAPI Code Generation Guide

This project supports automatic TypeScript client generation from the backend's Swagger/OpenAPI specification.

## Quick Start

1. **Start the backend** (required for generation):
   ```bash
   cd backend
   dotnet run --project WhatIsInMyFridge.Api
   ```

2. **Generate the TypeScript types**:
   ```bash
   cd frontend
   deno task generate-api
   ```

This will:
- Fetch the OpenAPI spec from `http://localhost:5000/swagger/v1/swagger.json`
- Generate fully typed TypeScript interfaces in `src/lib/api-types.ts`
- Provide full IntelliSense and type safety for all API endpoints

## Using the Generated Types

### Option 1: Enhanced Type Safety for Existing API

Update your `src/lib/api.ts` to import and use the generated types:

\`\`\`typescript
import type { paths } from "./api-types.ts";

// Example: Strongly type your response
type ItemsResponse = paths["/api/items"]["get"]["responses"]["200"]["content"]["application/json"];
type CreateItemRequest = paths["/api/items"]["post"]["requestBody"]["content"]["application/json"];

export async function listItems(): Promise<ItemsResponse> {
  const response = await fetch("/api/items");
  return await response.json();
}
\`\`\`

### Option 2: Full Type-Safe Client (Recommended)

Once you run `deno cache` or the dev server, you can use `openapi-fetch` for a fully type-safe client:

\`\`\`typescript
import createClient from "openapi-fetch";
import type { paths } from "./api-types.ts";

const client = createClient<paths>({ baseUrl: "" });

// Add auth middleware
client.use({
  onRequest({ request }) {
    const token = localStorage.getItem("auth_token");
    if (token) {
      request.headers.set("Authorization", \`Bearer \${token}\`);
    }
    return request;
  },
});

// Fully typed API calls with autocomplete!
const { data, error } = await client.GET("/api/items");
if (data) {
  data.forEach(item => {
    console.log(item.name); // ✅ TypeScript knows about all properties
  });
}

// POST with request body validation
const { data: newItem } = await client.POST("/api/items", {
  body: {
    name: "Milk",
    quantity: 1,
    location: "fridge",
    toBuy: false
  }
});
\`\`\`

## Workflow Integration

### During Development

Whenever you add/modify backend endpoints:

1. Update the C# code with proper DTOs and attributes
2. Run `deno task generate-api` to regenerate types
3. Frontend code will have updated types automatically

### CI/CD Integration

Add to your build pipeline:

\`\`\`bash
# Start backend in background
dotnet run --project backend/WhatIsInMyFridge.Api &
API_PID=$!

# Wait for API to be ready
sleep 5

# Generate types
cd frontend && deno task generate-api

# Kill background API
kill $API_PID

# Continue with frontend build
deno task build
\`\`\`

## Custom Configuration

### Change the API URL

If your backend runs on a different port:

\`\`\`bash
deno task generate-api http://localhost:8080/swagger/v1/swagger.json
\`\`\`

### Improve Swagger Documentation

Add XML documentation and annotations to your C# code:

\`\`\`csharp
/// <summary>
/// Creates a new food item in the inventory
/// </summary>
app.MapPost("/api/items", async Task<IResult> (...) => {
  // ...
})
.WithName("CreateFoodItem")
.WithTags("Inventory")
.WithOpenApi();
\`\`\`

## Benefits

✅ **Type Safety**: Catch API mismatches at compile time  
✅ **Autocomplete**: IntelliSense for all endpoints and models  
✅ **Single Source of Truth**: Backend defines the contract  
✅ **Refactoring**: Rename fields safely across frontend/backend  
✅ **Documentation**: Generated types serve as API documentation  

## Troubleshooting

### "Failed to fetch OpenAPI spec"
Make sure the backend is running on `http://localhost:5000`

### "Types not updating"
Delete `frontend/src/lib/api-types.ts` and run `deno task generate-api` again

### Import errors in VSCode
Restart the TypeScript server: `Ctrl+Shift+P` > "TypeScript: Restart TS Server"
