# Azure Bicep Deployment Script for WhatIsInMyFridge
# This script deploys the entire infrastructure to Azure using Bicep templates

$ErrorActionPreference = "Stop"

# Configuration
$RESOURCE_GROUP_NAME = if ($env:RESOURCE_GROUP_NAME) { $env:RESOURCE_GROUP_NAME } else { "whatsinmyfridge-rg" }
$LOCATION = if ($env:LOCATION) { $env:LOCATION } else { "austriaeast" }
$BICEP_FILE = "main.bicep"
$PARAMETERS_FILE = "main.parameters.json"

# Helper functions
function Write-Header($message) {
    Write-Host "`n===================================================" -ForegroundColor Blue
    Write-Host $message -ForegroundColor Blue
    Write-Host "===================================================`n" -ForegroundColor Blue
}

function Write-Success($message) {
    Write-Host "✓ $message" -ForegroundColor Green
}

function Write-Failure($message) {
    Write-Host "✗ $message" -ForegroundColor Red
}

function Write-Warning2($message) {
    Write-Host "⚠ $message" -ForegroundColor Yellow
}

function Write-Info($message) {
    Write-Host "ℹ $message" -ForegroundColor Cyan
}

# Check prerequisites
function Test-Prerequisites {
    Write-Header "Checking Prerequisites"
    
    # Check if Azure CLI is installed
    try {
        $null = az version 2>$null
        Write-Success "Azure CLI is installed"
    } catch {
        Write-Failure "Azure CLI is not installed. Please install it from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
        exit 1
    }
    
    # Check if logged in to Azure
    try {
        $account = az account show 2>$null | ConvertFrom-Json
        Write-Success "Logged in to Azure"
        
        # Display current subscription
        Write-Info "Current subscription: $($account.name) ($($account.id))"
    } catch {
        Write-Failure "Not logged in to Azure. Please run 'az login' first"
        exit 1
    }
    
    # Check if Bicep file exists
    if (!(Test-Path $BICEP_FILE)) {
        Write-Failure "Bicep file not found: $BICEP_FILE"
        exit 1
    }
    Write-Success "Bicep template found"
    
    # Check if parameters file exists
    if (!(Test-Path $PARAMETERS_FILE)) {
        Write-Failure "Parameters file not found: $PARAMETERS_FILE"
        exit 1
    }
    Write-Success "Parameters file found"
}

# Check JWT secret configuration
function Test-JwtSecret {
    Write-Header "JWT Secret Configuration"
    
    $content = Get-Content $PARAMETERS_FILE -Raw
    $params = $content | ConvertFrom-Json
    
    $jwtSecret = $params.parameters.jwtSecretKey.value
    
    if ($jwtSecret -eq "REPLACE_WITH_YOUR_JWT_SECRET" -or [string]::IsNullOrWhiteSpace($jwtSecret)) {
        Write-Warning2 "JWT secret needs to be configured"
        
        # Generate a random JWT secret
        $bytes = New-Object byte[] 32
        [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
        $GENERATED_SECRET = [Convert]::ToBase64String($bytes)
        
        Write-Host "`nGenerated JWT secret: " -NoNewline
        Write-Host $GENERATED_SECRET -ForegroundColor Green
        
        Write-Host "`nOptions:"
        Write-Host "1. Use the generated secret (recommended)"
        Write-Host "2. Enter your own secret"
        Write-Host "3. Set it manually later in Azure Portal"
        
        $choice = Read-Host "Choose an option (1-3)"
        
        switch ($choice) {
            "1" {
                $params.parameters.jwtSecretKey.value = $GENERATED_SECRET
                $params | ConvertTo-Json -Depth 10 | Set-Content $PARAMETERS_FILE
                Write-Success "Parameters file updated with generated JWT secret"
            }
            "2" {
                $USER_SECRET = Read-Host "Enter your JWT secret" -AsSecureString
                $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($USER_SECRET)
                $PlainSecret = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
                $params.parameters.jwtSecretKey.value = $PlainSecret
                $params | ConvertTo-Json -Depth 10 | Set-Content $PARAMETERS_FILE
                Write-Success "Parameters file updated with your JWT secret"
            }
            "3" {
                Write-Warning2 "You will need to set the JWT secret manually in Azure Portal after deployment"
                Write-Info "Navigate to: App Service > Configuration > Application settings > JWT_SECRET_KEY"
            }
            default {
                Write-Failure "Invalid option"
                exit 1
            }
        }
    } else {
        Write-Success "JWT secret is already configured"
    }
}

# Create resource group
function New-ResourceGroupIfNotExists {
    Write-Header "Creating Resource Group"
    
    $rg = az group show --name $RESOURCE_GROUP_NAME 2>$null
    
    if ($rg) {
        Write-Warning2 "Resource group '$RESOURCE_GROUP_NAME' already exists"
        $confirm = Read-Host "Do you want to continue and update existing resources? (y/n)"
        if ($confirm -ne "y") {
            Write-Info "Deployment cancelled"
            exit 0
        }
    } else {
        az group create `
            --name $RESOURCE_GROUP_NAME `
            --location $LOCATION `
            --output none
        Write-Success "Resource group '$RESOURCE_GROUP_NAME' created in $LOCATION"
    }
}

# Validate Bicep template
function Test-BicepTemplate {
    Write-Header "Validating Bicep Template"
    
    az deployment group validate `
        --resource-group $RESOURCE_GROUP_NAME `
        --template-file $BICEP_FILE `
        --parameters "@$PARAMETERS_FILE" `
        --output none
    
    Write-Success "Bicep template validation passed"
}

# Deploy infrastructure
function Start-InfrastructureDeployment {
    Write-Header "Deploying Infrastructure"
    
    Write-Info "This may take 5-10 minutes..."
    
    $deployment = az deployment group create `
        --resource-group $RESOURCE_GROUP_NAME `
        --template-file $BICEP_FILE `
        --parameters "@$PARAMETERS_FILE" `
        --output json | ConvertFrom-Json
    
    Write-Success "Infrastructure deployed successfully"
    
    return $deployment
}

# Display deployment outputs
function Show-DeploymentOutputs($deployment) {
    Write-Header "Deployment Outputs"
    
    $outputs = $deployment.properties.outputs
    
    if ($outputs.appServiceUrl) {
        Write-Host "Backend API: " -NoNewline
        Write-Host $outputs.appServiceUrl.value -ForegroundColor Green
    }
    
    if ($outputs.staticWebAppUrl -and $outputs.staticWebAppUrl.value) {
        Write-Host "Frontend: " -NoNewline
        Write-Host $outputs.staticWebAppUrl.value -ForegroundColor Green
    } else {
        Write-Warning2 "Static Web App not deployed (GitHub repo URL not configured)"
    }
    
    if ($outputs.cosmosEndpoint) {
        Write-Host "Cosmos DB Endpoint: " -NoNewline
        Write-Host $outputs.cosmosEndpoint.value -ForegroundColor Green
    }
    
    if ($outputs.storageBlobEndpoint -and $outputs.storageBlobEndpoint.value) {
        Write-Host "Blob Storage Endpoint: " -NoNewline
        Write-Host $outputs.storageBlobEndpoint.value -ForegroundColor Green
    }
    
    Write-Host ""
}

# Display next steps
function Show-NextSteps($deployment) {
    Write-Header "Next Steps"
    
    $outputs = $deployment.properties.outputs
    $appServiceName = $outputs.appServiceName.value
    $staticWebAppName = if ($outputs.staticWebAppName) { $outputs.staticWebAppName.value } else { $null }
    
    Write-Host "1. Deploy the backend API:"
    Write-Host "   cd ..\backend\WhatIsInMyFridge.Api" -ForegroundColor Yellow
    Write-Host "   dotnet publish -c Release -o .\publish" -ForegroundColor Yellow
    Write-Host "   Compress-Archive -Path .\publish\* -DestinationPath .\deploy.zip -Force" -ForegroundColor Yellow
    Write-Host "   az webapp deploy --resource-group $RESOURCE_GROUP_NAME --name $appServiceName --src-path .\deploy.zip --type zip" -ForegroundColor Yellow
    Write-Host ""
    
    if ($staticWebAppName) {
        Write-Host "2. Frontend will be automatically deployed via GitHub Actions"
        Write-Host "   View deployment status: https://portal.azure.com" -ForegroundColor Yellow
    } else {
        Write-Host "2. Deploy the frontend manually or configure GitHub integration:"
        Write-Host "   cd ..\frontend" -ForegroundColor Yellow
        Write-Host "   deno task build" -ForegroundColor Yellow
        Write-Host "   az staticwebapp create ... (or configure in Azure Portal)" -ForegroundColor Yellow
    }
    Write-Host ""
    
    Write-Host "3. Configure custom domains (optional):"
    Write-Host "   Backend:  az webapp config hostname add ..." -ForegroundColor Yellow
    Write-Host "   Frontend: az staticwebapp hostname set ..." -ForegroundColor Yellow
    Write-Host ""
    
    Write-Host "4. Monitor your application:"
    Write-Host "   https://portal.azure.com" -ForegroundColor Yellow
    Write-Host ""
    
    Write-Success "Deployment complete!"
}

# Main deployment flow
function Start-Deployment {
    Write-Header "WhatIsInMyFridge - Azure Deployment"
    
    Test-Prerequisites
    Test-JwtSecret
    New-ResourceGroupIfNotExists
    Test-BicepTemplate
    $deployment = Start-InfrastructureDeployment
    Show-DeploymentOutputs $deployment
    Show-NextSteps $deployment
}

# Run main function
Start-Deployment
