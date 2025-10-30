# WhatIsInMyFridge - Session History

## Session 2: Azure Cosmos DB Migration (Completed)

**Date:** October 30, 2025  
**Status:** ✅ Complete

### Overview
Successfully migrated the entire backend from SQLite to Azure Cosmos DB (NoSQL) with Azure Blob Storage support for recipe photos. The application is now cloud-ready and can be deployed to Azure using free tier resources.

---

### Backend Changes (All Complete ✅)

#### Files Modified:
1. `backend/WhatIsInMyFridge.Api/Program.cs` - Updated for Cosmos DB and BlobStorageService integration
2. `backend/WhatIsInMyFridge.Api/Services/BlobStorageService.cs` - Created (hybrid cloud/local photo storage)
3. `backend/WhatIsInMyFridge.Api/Services/AppDbContext.cs` - Already configured for Cosmos DB
4. `backend/WhatIsInMyFridge.Api/appsettings.json` - Added Cosmos DB and Blob Storage configuration
5. `backend/WhatIsInMyFridge.Api/appsettings.Development.json` - Added Cosmos DB Emulator connection
6. `backend/WhatIsInMyFridge.Api/WhatIsInMyFridge.Api.csproj` - Updated to Cosmos DB packages
7. `DEPLOYMENT.md` - Completely rewritten for Azure deployment

#### Files Removed:
- `/backend/WhatIsInMyFridge.Api/Migrations/` - Deleted (Cosmos DB is schema-less, no migrations needed)

#### Key Backend Updates:
- ✅ Removed SQLite packages, added `Microsoft.EntityFrameworkCore.Cosmos` and Azure.Storage.Blobs packages
- ✅ Configured Cosmos DB with proper partition keys:
  - Users/Households: partitioned by `Id`
  - FoodItems/Recipes/GroceryItems: partitioned by `HouseholdId`
  - RecipeIngredients: partitioned by `RecipeId`
- ✅ Changed database initialization from `Migrate()` to `EnsureCreatedAsync()` for Cosmos DB
- ✅ Created `BlobStorageService` with hybrid storage approach:
  - Uses Azure Blob Storage when connection string is configured
  - Falls back to local filesystem when Blob Storage is not configured
  - Allows development without Azure resources
- ✅ Updated photo upload endpoint (`/api/recipes/{id}/photos`) to use BlobStorageService
- ✅ Updated photo deletion endpoint (`/api/recipes/{id}/photos/{photoId}`) to use BlobStorageService
- ✅ Updated photo retrieval endpoint (`/api/photos/{fileName}`) to use BlobStorageService
- ✅ Added Cosmos DB Emulator connection string to development config
- ✅ Configured CORS for custom domain (`fridge.colen.at`, `colen.at`)
- ✅ **Backend builds successfully** (`dotnet build` succeeded with 0 errors, 0 warnings)

---

### Database Architecture

#### Cosmos DB Containers:
1. **Users** - Partition Key: `/Id`
2. **Households** - Partition Key: `/Id`
3. **FoodItems** - Partition Key: `/HouseholdId`
4. **Recipes** - Partition Key: `/HouseholdId`
5. **RecipeIngredients** - Partition Key: `/RecipeId`
6. **GroceryItems** - Partition Key: `/HouseholdId`

#### Benefits of Cosmos DB:
- ✅ Global distribution capability
- ✅ Automatic scaling
- ✅ 1000 RU/s + 25GB storage free forever
- ✅ Multi-model support (SQL API used)
- ✅ Automatic indexing
- ✅ Schema-less (no migrations needed)
- ✅ Better multi-tenant isolation via partition keys

---

### Photo Storage Architecture

#### BlobStorageService Features:
- **Hybrid Mode**: Automatically detects if Azure Blob Storage is configured
- **Cloud Storage**: Uses Azure Blob Storage when `BLOB_STORAGE_CONNECTION_STRING` is set
- **Local Fallback**: Uses local filesystem (`/app/data/photos`) when Blob Storage not configured
- **Seamless**: No code changes needed between development and production
- **Photo Operations**:
  - `UploadPhotoAsync()` - Uploads to blob storage or local filesystem
  - `DeletePhotoAsync()` - Deletes by photoId (tries all extensions: .jpg, .jpeg, .png, .webp)
  - `GetPhotoAsync()` - Retrieves photo with content type
  - `IsUsingBlobStorage` - Property to check which storage mode is active

#### Endpoint Updates:
1. **Upload** (`POST /api/recipes/{id}/photos`):
   - Validates file size (5MB limit)
   - Validates file type (JPEG, PNG, WebP only)
   - Generates unique photoId
   - Uploads via BlobStorageService
   - Returns photoId and URL

2. **Delete** (`DELETE /api/recipes/{id}/photos/{photoId}`):
   - Uses BlobStorageService to delete by photoId
   - Handles multiple file extensions automatically
   - Updates recipe metadata

3. **Retrieve** (`GET /api/photos/{fileName}`):
   - Uses BlobStorageService to fetch photo
   - Returns proper content type
   - Works with both blob storage and local files

---

### Configuration

#### Production Configuration (`appsettings.json`):
```json
{
  "CosmosDb": {
    "ConnectionString": "",
    "DatabaseName": "WhatIsInMyFridge"
  },
  "BlobStorage": {
    "ConnectionString": "",
    "ContainerName": "recipe-photos"
  }
}
```

#### Development Configuration (`appsettings.Development.json`):
```json
{
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
  }
}
```

#### Environment Variables:
- `COSMOS_CONNECTION_STRING` - Overrides CosmosDb:ConnectionString
- `BLOB_STORAGE_CONNECTION_STRING` - Overrides BlobStorage:ConnectionString (optional)
- `JWT_SECRET_KEY` - JWT signing key (existing)

---

### Deployment Guide Updates

#### New Deployment Documentation (`DEPLOYMENT.md`):
- ✅ Complete Azure deployment guide
- ✅ Step-by-step instructions for Cosmos DB free tier
- ✅ Azure Blob Storage setup (optional)
- ✅ Azure App Service deployment (F1 free tier)
- ✅ Azure Static Web Apps for frontend
- ✅ Custom domain configuration for `colen.at`
- ✅ Cosmos DB Emulator setup for local development
- ✅ Troubleshooting guide for Azure-specific issues
- ✅ Backup and recovery procedures
- ✅ Migration guide from SQLite to Cosmos DB
- ✅ Quick deployment script with all Azure CLI commands
- ✅ Cost breakdown and free tier limits

#### Azure Free Tier Resources:
1. **Cosmos DB**: 1000 RU/s + 25GB free forever (one per subscription)
2. **App Service**: F1 free tier (60 CPU min/day, 1GB RAM)
3. **Static Web Apps**: 100GB bandwidth/month, unlimited deployments
4. **Blob Storage**: First 5GB free for 12 months, then ~$0.02/GB/month

**Total Monthly Cost**: $0 (using free tiers)

---

### Current State

✅ **Backend:** Fully migrated to Cosmos DB, builds successfully  
✅ **Photo Storage:** Hybrid BlobStorageService created and integrated  
✅ **All endpoints updated:** Photo upload, deletion, and retrieval use BlobStorageService  
✅ **Configuration:** Development and production configs ready  
✅ **Documentation:** Complete Azure deployment guide created  
✅ **Build Status:** 0 errors, 0 warnings  

**Known Non-Issues:**
- IDE (OmniSharp) shows false errors about `UseCosmos` extension method - actual `dotnet build` succeeds
- Azure SDK errors in IDE - packages are installed correctly and build succeeds

---

### Testing Instructions

#### Local Development with Cosmos DB Emulator:

**Option A: Use Cosmos DB Emulator**
```bash
# Install Cosmos DB Emulator (macOS/Linux via Docker)
docker run -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254 \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator

# Start backend
cd backend/WhatIsInMyFridge.Api
dotnet run
```

**Option B: Use Azure Free Tier Resources**
```bash
# Set environment variables
export COSMOS_CONNECTION_STRING="your-azure-cosmos-connection-string"
export BLOB_STORAGE_CONNECTION_STRING="your-azure-blob-storage-connection-string"  # optional

# Start backend
cd backend/WhatIsInMyFridge.Api
dotnet run
```

#### Test Photo Upload:
```bash
# Upload a photo
curl -X POST http://localhost:5000/api/recipes/{recipeId}/photos \
  -H "Authorization: Bearer {token}" \
  -F "file=@photo.jpg"

# Retrieve photo
curl http://localhost:5000/api/photos/{photoId}.jpg

# Delete photo
curl -X DELETE http://localhost:5000/api/recipes/{recipeId}/photos/{photoId} \
  -H "Authorization: Bearer {token}"
```

---

### Migration from SQLite to Cosmos DB

#### Breaking Changes:
1. **Database:** SQLite → Cosmos DB (NoSQL)
2. **Migrations:** No longer needed (schema-less)
3. **Connection String:** Different format
4. **Partition Keys:** Required for optimal performance
5. **Data Structure:** Same models, different storage engine

#### Migration Steps:
1. Export SQLite data to JSON
2. Import JSON data to Cosmos DB using migration script or Azure Data Migration Tool
3. Verify all data imported correctly
4. Update connection strings
5. Deploy new backend

See `DEPLOYMENT.md` for detailed migration instructions.

---

### Azure Deployment Steps

#### Quick Deploy to Azure:
```bash
# Login to Azure
az login

# Create Cosmos DB (free tier)
az cosmosdb create \
  --name whatsinmyfridge \
  --resource-group WhatIsInMyFridge-RG \
  --enable-free-tier true

# Create Blob Storage (optional)
az storage account create \
  --name whatsinmyfridgestorage \
  --resource-group WhatIsInMyFridge-RG \
  --sku Standard_LRS

# Create App Service (F1 free tier)
az webapp create \
  --name whatsinmyfridge-api \
  --resource-group WhatIsInMyFridge-RG \
  --plan WhatIsInMyFridge-Plan \
  --runtime "DOTNET:9.0"

# Set environment variables
az webapp config appsettings set \
  --name whatsinmyfridge-api \
  --resource-group WhatIsInMyFridge-RG \
  --settings \
    COSMOS_CONNECTION_STRING="..." \
    BLOB_STORAGE_CONNECTION_STRING="..." \
    JWT_SECRET_KEY="..."

# Deploy via GitHub Actions or local git
```

See `DEPLOYMENT.md` for complete deployment instructions.

---

### Files Modified Summary

**Backend (7 files):**
- Program.cs - Cosmos DB config, BlobStorageService integration, photo endpoints
- Services/BlobStorageService.cs (new) - Hybrid cloud/local photo storage
- Services/AppDbContext.cs - Already had Cosmos DB configuration
- appsettings.json - Cosmos DB and Blob Storage config
- appsettings.Development.json - Cosmos DB Emulator connection
- WhatIsInMyFridge.Api.csproj - Updated packages
- /Migrations/ (deleted) - No longer needed

**Documentation (1 file):**
- DEPLOYMENT.md - Completely rewritten for Azure deployment

**No changes needed:**
- Frontend files (no changes required)
- All model files (compatible with both SQLite and Cosmos DB)
- Store files (use EF Core, works with both databases)

---

### Next Steps & Future Work

**Immediate Deployment:**
- [ ] Create Azure Cosmos DB free tier account
- [ ] (Optional) Create Azure Blob Storage account
- [ ] Deploy backend to Azure App Service
- [ ] Deploy frontend to Azure Static Web Apps
- [ ] Configure custom domain (fridge.colen.at)
- [ ] Test full application on Azure

**Monitoring & Optimization:**
- [ ] Set up Application Insights for telemetry
- [ ] Monitor Cosmos DB RU consumption
- [ ] Optimize queries for better performance
- [ ] Set up automated backups for Cosmos DB
- [ ] Configure CDN for photo delivery (optional)

**Feature Enhancements:**
- [ ] Add photo resizing/optimization before upload
- [ ] Implement photo thumbnails
- [ ] Add bulk photo upload
- [ ] Implement photo galleries
- [ ] Add photo metadata (caption, alt text)

**Production Readiness:**
- [ ] Configure custom domain SSL certificates
- [ ] Set up monitoring and alerting
- [ ] Implement rate limiting
- [ ] Add comprehensive logging
- [ ] Add unit and integration tests for Cosmos DB operations
- [ ] Create disaster recovery plan

---

## Troubleshooting Guide

### Cosmos DB Issues:

**Issue:** Connection string errors  
**Solution:** Verify connection string format and that Cosmos DB account is created

**Issue:** Partition key errors  
**Solution:** Ensure models have correct partition key properties matching AppDbContext configuration

**Issue:** RU consumption too high  
**Solution:** Optimize queries, add indexes, or upgrade from free tier

### Blob Storage Issues:

**Issue:** Photo upload fails  
**Solution:** Check if BLOB_STORAGE_CONNECTION_STRING is valid (or omit for local storage)

**Issue:** Photos not loading  
**Solution:** Verify BlobStorageService is returning correct content type and data

**Issue:** Cannot delete photos  
**Solution:** Ensure photoId is correct and file exists in blob storage or local filesystem

### Deployment Issues:

**Issue:** Backend won't start on Azure  
**Solution:** Check App Service logs, verify environment variables are set

**Issue:** Database not initializing  
**Solution:** Ensure EnsureCreatedAsync() is called on startup and Cosmos DB is accessible

---

Last Updated: October 30, 2025  
Next Review: After Azure deployment and testing

---

## Session 1: JWT Authentication Migration (Completed)

**Date:** Previous session  
**Status:** ✅ Complete

### Overview
Successfully migrated the entire application from session-based authentication to JWT (JSON Web Token) authentication.

---

### Backend Changes (All Complete ✅)

#### Files Modified:
1. `backend/WhatIsInMyFridge.Api/Program.cs` - Replaced all session-based auth with JWT
2. `backend/WhatIsInMyFridge.Api/Services/JwtTokenService.cs` - Created (generates JWT tokens)
3. `backend/WhatIsInMyFridge.Api/appsettings.json` - Added JWT configuration
4. `backend/WhatIsInMyFridge.Api/appsettings.Development.json` - Added JWT configuration
5. `backend/WhatIsInMyFridge.Api/WhatIsInMyFridge.Api.csproj` - Added JWT package dependency

#### Key Backend Updates:
- ✅ Installed `Microsoft.AspNetCore.Authentication.JwtBearer` v8.0.11
- ✅ Configured JWT authentication with token validation parameters (issuer, audience, signing key)
- ✅ Removed all session middleware
- ✅ Updated ALL endpoints to use JWT claims instead of session:
  - Replaced `httpContext.Session.GetString("userId")` with `httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value`
  - Replaced `httpContext.Session.GetString("householdId")` with `httpContext.User.FindFirst("householdId")?.Value`
  - Added `.RequireAuthorization()` to all protected endpoints
- ✅ Auth endpoints (`/api/auth/login`, `/api/auth/register`) now return JWT tokens
- ✅ Household switching endpoint returns new JWT token with updated householdId claim
- ✅ **Backend builds successfully** (`dotnet build` succeeded with 0 errors)

---

### Frontend Changes (All Complete ✅)

#### Files Modified:
1. `frontend/src/lib/types.ts` - Added `token: string` to AuthResponse interface
2. `frontend/src/lib/auth.ts` - Added localStorage token management functions (getToken, setToken, clearToken)
3. `frontend/src/lib/api.ts` - Updated to send `Authorization: Bearer <token>` header with all requests
4. `frontend/src/lib/Login.svelte` - Updated to store JWT token on login
5. `frontend/src/lib/Register.svelte` - Updated to store JWT token on registration

#### Key Frontend Updates:
- ✅ Token stored in `localStorage` with key "auth_token"
- ✅ All API requests automatically include Authorization header if token exists
- ✅ Removed `credentials: "include"` (no longer needed for cookies)
- ✅ Login/Register flows now call `setToken(response.token)` before updating auth store
- ✅ Logout clears token from localStorage
- ✅ `api-client.ts` (optional OpenAPI client) already had JWT support

---

### Current State

✅ **Backend:** Fully migrated to JWT, builds successfully  
✅ **Frontend:** Fully migrated to JWT, properly stores and sends tokens  
✅ **All endpoints updated:** Every API endpoint now uses JWT authorization  

**Known Non-Issues:**
- `deno task check` shows Vite/Node type definition warnings (expected in Deno environment)
- Missing `api-types.ts` (optional OpenAPI-generated types, not required for app to function)

---

### Testing Instructions

To test the full authentication flow:

```bash
# Terminal 1: Start backend
cd backend/WhatIsInMyFridge.Api
dotnet run

# Terminal 2: Start frontend
cd frontend
deno task dev
```

**Manual Test Steps:**
1. Navigate to http://localhost:5173
2. Test registration with new user
3. Test login
4. Test creating/viewing food items
5. Test logout (verify token cleared from localStorage)
6. Verify 401 responses when accessing protected routes without token

---

### Security Considerations

**Current Implementation:**
- JWT secret stored in `appsettings.json` and `appsettings.Development.json`
- Tokens expire after 7 days
- Token includes userId and householdId claims
- All protected endpoints require valid JWT

**Recommended Future Enhancements:**
1. Move JWT secret to environment variables or user secrets (more secure)
2. Implement refresh tokens for longer sessions
3. Add token expiration handling in frontend (redirect to login on 401)
4. Consider shorter token expiration times with refresh token mechanism
5. Add token revocation mechanism
6. Implement HTTPS in production

---

### Architecture Details

**JWT Token Structure:**
```json
{
  "sub": "userId",
  "householdId": "householdId",
  "exp": "expirationTimestamp",
  "iss": "WhatIsInMyFridge.Api",
  "aud": "WhatIsInMyFridge.Client"
}
```

**Token Flow:**
1. User logs in or registers → Backend validates credentials
2. Backend generates JWT with userId and householdId claims
3. Frontend receives token in response body
4. Frontend stores token in localStorage
5. All subsequent API requests include `Authorization: Bearer <token>` header
6. Backend validates token on each request and extracts claims
7. Logout clears token from localStorage

**Household Switching:**
- When user switches household, backend generates new JWT with updated householdId
- Frontend must update stored token with new value
- Old token becomes invalid for household-scoped operations

---

### Files Modified Summary

**Backend (5 files):**
- Program.cs
- Services/JwtTokenService.cs (new)
- appsettings.json
- appsettings.Development.json
- WhatIsInMyFridge.Api.csproj

**Frontend (5 files):**
- src/lib/types.ts
- src/lib/auth.ts
- src/lib/api.ts
- src/lib/Login.svelte
- src/lib/Register.svelte

**No changes needed:**
- App.svelte
- api-client.ts
- Inventory.svelte

---

### Next Steps & Future Work

**Immediate Testing Needed:**
- [ ] Full end-to-end authentication flow testing
- [ ] Verify all protected endpoints require valid JWT
- [ ] Test token expiration handling
- [ ] Test household switching with new JWT generation

**Security Improvements:**
- [ ] Move JWT secret to environment variables
- [ ] Implement refresh token mechanism
- [ ] Add proper error handling for expired tokens in frontend
- [ ] Add automatic token refresh before expiration
- [ ] Implement token revocation/blacklisting

**Feature Enhancements:**
- [ ] Add "Remember Me" functionality with longer-lived refresh tokens
- [ ] Implement household switching UI in frontend
- [ ] Add profile management features
- [ ] Implement password reset functionality

**Production Readiness:**
- [ ] Configure HTTPS
- [ ] Set up proper secret management
- [ ] Add rate limiting on auth endpoints
- [ ] Add logging and monitoring for authentication events
- [ ] Add unit and integration tests for auth flow

---

## Migration Notes

### Breaking Changes from Session to JWT:
- Frontend must now manage token storage
- Cookies no longer used for authentication
- All authenticated requests must include Authorization header
- Token must be refreshed when switching households

### Backward Compatibility:
- Not backward compatible with session-based authentication
- Requires coordinated deployment of backend and frontend
- Existing sessions will be invalidated

---

## Troubleshooting Guide

**Issue:** 401 Unauthorized on all protected endpoints  
**Solution:** Verify token is stored in localStorage and Authorization header is being sent

**Issue:** Token not being sent with requests  
**Solution:** Check that `getToken()` is called before each request in `api.ts`

**Issue:** Token validation fails  
**Solution:** Verify JWT configuration in appsettings.json matches between backend and token generation

**Issue:** Household data not loading  
**Solution:** Verify householdId claim is present in JWT token

---

Last Updated: Current session  
Next Review: After initial testing phase
