# Container Apps Deployment Scripts

Scripts to deploy container images to Azure Container Apps.

## Prerequisites

- Azure CLI installed and logged in (`az login`)
- Appropriate permissions to update Container Apps in the target resource group
- Container images already published to the container registry

## Scripts

### Bash (Linux/macOS)

```bash
./deploy/scripts/deploy-container-apps.sh --tag develop-a13130d
```

### PowerShell (Windows)

```powershell
.\deploy\scripts\deploy-container-apps.ps1 -Tag develop-a13130d
```

## Usage

### Basic deployment with default settings

**Bash:**
```bash
./deploy/scripts/deploy-container-apps.sh --tag <image-tag>
```

**PowerShell:**
```powershell
.\deploy\scripts\deploy-container-apps.ps1 -Tag <image-tag>
```

### Deploy to production environment

**Bash:**
```bash
./deploy/scripts/deploy-container-apps.sh \
  --tag develop-fe37316 \
  --resource-group rg-whatismyfridge-prod \
  --backend-app ca-backend-prod \
  --frontend-app ca-frontend-prod
```

**PowerShell:**
```powershell
.\deploy\scripts\deploy-container-apps.ps1 `
  -Tag develop-fe37316 `
  -ResourceGroup rg-whatismyfridge-prod `
  -BackendApp ca-backend-prod `
  -FrontendApp ca-frontend-prod
```

### Deploy backend only

**Bash:**
```bash
./deploy/scripts/deploy-container-apps.sh --tag <image-tag> --skip-frontend
```

**PowerShell:**
```powershell
.\deploy\scripts\deploy-container-apps.ps1 -Tag <image-tag> -SkipFrontend
```

### Custom registry

**Bash:**
```bash
./deploy/scripts/deploy-container-apps.sh \
  --tag <image-tag> \
  --registry-prefix myregistry.azurecr.io/myapp
```

**PowerShell:**
```powershell
.\deploy\scripts\deploy-container-apps.ps1 `
  -Tag <image-tag> `
  -RegistryPrefix myregistry.azurecr.io/myapp
```

## Parameters

| Bash | PowerShell | Description | Default |
|------|------------|-------------|---------|
| `--tag` | `-Tag` | Container image tag (required) | - |
| `--resource-group` | `-ResourceGroup` | Azure resource group | `rg-whatismyfridge-test` |
| `--backend-app` | `-BackendApp` | Backend container app name | `ca-backend-test` |
| `--frontend-app` | `-FrontendApp` | Frontend container app name | `ca-frontend-test` |
| `--registry-prefix` | `-RegistryPrefix` | Image prefix without tag | `ghcr.io/lenny32/whatisinmyfridge` |
| `--skip-frontend` | `-SkipFrontend` | Update backend only | `false` |
| `--help` | `-Help` | Show help message | - |

## What the script does

1. **Validates** Azure CLI is installed and parameters are provided
2. **Retrieves** the frontend FQDN for CORS configuration
3. **Updates backend** container app with:
   - New image tag
   - CORS allowed origins
   - Required secret references
4. **Retrieves** the backend FQDN
5. **Updates frontend** container app with:
   - New image tag
   - Backend API URL
6. **Outputs** the deployment results with FQDNs

## Environment Variables

The scripts configure the following environment variables on the container apps:

### Backend
- `ALLOWED_ORIGINS` - Frontend URL for CORS
- `BLOB_STORAGE_CONNECTION_STRING` - Secret reference
- `COSMOS_CONNECTION_STRING` - Secret reference

### Frontend
- `VITE_API_BASE` - Backend API URL

## Getting Help

**Bash:**
```bash
./deploy/scripts/deploy-container-apps.sh --help
```

**PowerShell:**
```powershell
Get-Help .\deploy\scripts\deploy-container-apps.ps1 -Detailed
```
