#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Deploys container apps to Azure Container Apps.

.DESCRIPTION
    Updates Azure Container Apps with new container images from the registry.

.PARAMETER Tag
    Container image tag to deploy (e.g. develop-a13130d). Required.

.PARAMETER ResourceGroup
    Azure resource group (default: rg-whatismyfridge-test)

.PARAMETER BackendApp
    Container Apps backend name (default: ca-backend-test)

.PARAMETER FrontendApp
    Container Apps frontend name (default: ca-frontend-test)

.PARAMETER RegistryPrefix
    Image prefix without tag (default: ghcr.io/lenny32/whatisinmyfridge)

.PARAMETER SkipFrontend
    Update backend only

.PARAMETER Help
    Show help message

.EXAMPLE
    .\deploy-container-apps.ps1 -Tag develop-a13130d

.EXAMPLE
    .\deploy-container-apps.ps1 -Tag develop-a13130d -SkipFrontend

.NOTES
    Azure login must be completed before running.
    AZURE_CORE_OUTPUT environment variable can be set to control CLI output format.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Tag,

    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "rg-whatismyfridge-test",

    [Parameter(Mandatory=$false)]
    [string]$BackendApp = "ca-backend-test",

    [Parameter(Mandatory=$false)]
    [string]$FrontendApp = "ca-frontend-test",

    [Parameter(Mandatory=$false)]
    [string]$RegistryPrefix = "ghcr.io/lenny32/whatisinmyfridge",

    [Parameter(Mandatory=$false)]
    [switch]$SkipFrontend,

    [Parameter(Mandatory=$false)]
    [switch]$Help
)

$ErrorActionPreference = "Stop"

function Show-Usage {
    Get-Help $PSCommandPath -Detailed
}

if ($Help) {
    Show-Usage
    exit 0
}

# Check if Azure CLI is available
$azCommand = Get-Command az -ErrorAction SilentlyContinue
if (-not $azCommand) {
    Write-Error "Azure CLI is required but not found in PATH."
    exit 1
}

$revisionSuffix = "$(Get-Date -Format 'yyyyMMddHHmmss')-$Tag"
$backendImage = "$RegistryPrefix/backend:$Tag"
$frontendImage = "$RegistryPrefix/frontend:$Tag"

Write-Host "Updating backend container app $BackendApp with image $backendImage"

# Get frontend FQDN for CORS configuration
$frontendFqdn = ""
try {
    $frontendFqdn = az containerapp show `
        --name $FrontendApp `
        --resource-group $ResourceGroup `
        --query properties.configuration.ingress.fqdn `
        --output tsv 2>$null
} catch {
    # Frontend FQDN not available, continue without it
}

$allowedOrigins = ""
if (-not [string]::IsNullOrWhiteSpace($frontendFqdn)) {
    $allowedOrigins = "https://$frontendFqdn"
}

# Update backend container app
az containerapp update `
    --name $BackendApp `
    --resource-group $ResourceGroup `
    --image $backendImage `
    --revision-suffix $revisionSuffix `
    --set-env-vars `
        "ALLOWED_ORIGINS=$allowedOrigins" `
        "BLOB_STORAGE_CONNECTION_STRING=secretref:blob-storage-connection-string" `
        "COSMOS_CONNECTION_STRING=secretref:cosmos-connection-string" `
    | Out-Null

# Get backend FQDN
$backendFqdn = az containerapp show `
    --name $BackendApp `
    --resource-group $ResourceGroup `
    --query properties.configuration.ingress.fqdn `
    --output tsv

Write-Host "Backend revision updated. Current FQDN: https://$backendFqdn"

if (-not $SkipFrontend) {
    Write-Host "Updating frontend container app $FrontendApp with image $frontendImage"
    
    az containerapp update `
        --name $FrontendApp `
        --resource-group $ResourceGroup `
        --image $frontendImage `
        --revision-suffix $revisionSuffix `
        --set-env-vars `
            "VITE_API_BASE=https://$backendFqdn" `
        | Out-Null

    $frontendFqdn = az containerapp show `
        --name $FrontendApp `
        --resource-group $ResourceGroup `
        --query properties.configuration.ingress.fqdn `
        --output tsv

    Write-Host "Frontend revision updated. Current FQDN: https://$frontendFqdn"
} else {
    Write-Host "Frontend update skipped."
}

Write-Host "Deployment complete."
