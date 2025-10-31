#!/bin/bash

# Azure Infrastructure Setup Script for WhatIsInMyFridge
# This script creates cost-optimized Azure Container Apps infrastructure for Test and Prod environments

set -e

# Configuration
LOCATION="eastus"  # Change to your preferred region
SUBSCRIPTION_ID="${AZURE_SUBSCRIPTION_ID}"  # Set this environment variable or replace with your subscription ID

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to create resources for an environment
create_environment() {
    local ENV=$1
    local RG_NAME="rg-whatismyfridge-${ENV}"
    local LOG_ANALYTICS="logs-whatismyfridge-${ENV}"
    local CONTAINER_ENV="cae-whatismyfridge-${ENV}"
    local BACKEND_APP="ca-backend-${ENV}"
    local FRONTEND_APP="ca-frontend-${ENV}"
    local FILE_SHARE="items-${ENV}"
    local STORAGE_ACCOUNT="sawhatismyfridge${ENV}"

    print_info "Creating resources for ${ENV} environment..."

    # Create Resource Group
    print_info "Creating resource group: ${RG_NAME}"
    az group create \
        --name "${RG_NAME}" \
        --location "${LOCATION}"

    # Create Log Analytics Workspace (required for Container Apps)
    print_info "Creating Log Analytics workspace: ${LOG_ANALYTICS}"
    az monitor log-analytics workspace create \
        --resource-group "${RG_NAME}" \
        --workspace-name "${LOG_ANALYTICS}" \
        --location "${LOCATION}"

    LOG_ANALYTICS_ID=$(az monitor log-analytics workspace show \
        --resource-group "${RG_NAME}" \
        --workspace-name "${LOG_ANALYTICS}" \
        --query customerId \
        --output tsv)

    LOG_ANALYTICS_KEY=$(az monitor log-analytics workspace get-shared-keys \
        --resource-group "${RG_NAME}" \
        --workspace-name "${LOG_ANALYTICS}" \
        --query primarySharedKey \
        --output tsv)

    # Create Container Apps Environment (Consumption plan - cheapest option)
    print_info "Creating Container Apps Environment: ${CONTAINER_ENV}"
    az containerapp env create \
        --name "${CONTAINER_ENV}" \
        --resource-group "${RG_NAME}" \
        --location "${LOCATION}" \
        --logs-workspace-id "${LOG_ANALYTICS_ID}" \
        --logs-workspace-key "${LOG_ANALYTICS_KEY}"

    # Create Storage Account for persistent data (items.json)
    print_info "Creating Storage Account: ${STORAGE_ACCOUNT}"
    az storage account create \
        --name "${STORAGE_ACCOUNT}" \
        --resource-group "${RG_NAME}" \
        --location "${LOCATION}" \
        --sku Standard_LRS \
        --kind StorageV2 \
        --access-tier Hot

    # Get storage account key
    STORAGE_KEY=$(az storage account keys list \
        --resource-group "${RG_NAME}" \
        --account-name "${STORAGE_ACCOUNT}" \
        --query "[0].value" \
        --output tsv)

    # Create File Share for items.json
    print_info "Creating Azure File Share: ${FILE_SHARE}"
    az storage share create \
        --name "${FILE_SHARE}" \
        --account-name "${STORAGE_ACCOUNT}" \
        --account-key "${STORAGE_KEY}" \
        --quota 1

    # Create storage mount in Container Apps Environment
    print_info "Creating storage mount in Container Apps Environment"
    az containerapp env storage set \
        --name "${CONTAINER_ENV}" \
        --resource-group "${RG_NAME}" \
        --storage-name "items-storage" \
        --azure-file-account-name "${STORAGE_ACCOUNT}" \
        --azure-file-account-key "${STORAGE_KEY}" \
        --azure-file-share-name "${FILE_SHARE}" \
        --access-mode ReadWrite

    # Create Backend Container App (0.25 vCPU, 0.5 GB memory - minimum)
    print_info "Creating Backend Container App: ${BACKEND_APP}"
    az containerapp create \
        --name "${BACKEND_APP}" \
        --resource-group "${RG_NAME}" \
        --environment "${CONTAINER_ENV}" \
        --image "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest" \
        --target-port 8080 \
        --ingress external \
        --cpu 0.25 \
        --memory 0.5Gi \
        --min-replicas 0 \
        --max-replicas 2 \
        --scale-rule-name "http-scale" \
        --scale-rule-type "http" \
        --scale-rule-http-concurrency 10

    # Set secrets for backend (you need to update these with real values)
    print_warning "Setting placeholder secrets - YOU MUST UPDATE THESE!"
    az containerapp secret set \
        --name "${BACKEND_APP}" \
        --resource-group "${RG_NAME}" \
        --secrets \
            cosmos-connection-string="REPLACE_WITH_YOUR_COSMOS_CONNECTION_STRING" \
            blob-storage-connection-string="REPLACE_WITH_YOUR_BLOB_STORAGE_CONNECTION_STRING" \
            jwt-secret-key="$(openssl rand -base64 64 | tr -d '\n')"

    # Get frontend URL (will be created later)
    # We'll update CORS after frontend is created

    # Mount storage to backend
    print_info "Configuring backend with storage and environment variables"
    
    # Get frontend URL for CORS
    FRONTEND_URL=$(az containerapp show \
        --name "${FRONTEND_APP}" \
        --resource-group "${RG_NAME}" \
        --query properties.configuration.ingress.fqdn \
        --output tsv)
    
    az containerapp update \
        --name "${BACKEND_APP}" \
        --resource-group "${RG_NAME}" \
        --set-env-vars \
            "ASPNETCORE_ENVIRONMENT=$([[ ${ENV} == 'prod' ]] && echo 'Production' || echo 'Development')" \
            "COSMOS_CONNECTION_STRING=secretref:cosmos-connection-string" \
            "BLOB_STORAGE_CONNECTION_STRING=secretref:blob-storage-connection-string" \
            "JWT_SECRET_KEY=secretref:jwt-secret-key" \
            "ALLOWED_ORIGINS=https://${FRONTEND_URL}" \
        --volume-mount "items-volume:/app/data" \
        --volume-name "items-volume" \
        --volume-storage-name "items-storage" \
        --volume-storage-type "AzureFile"

    # Get backend URL
    BACKEND_URL=$(az containerapp show \
        --name "${BACKEND_APP}" \
        --resource-group "${RG_NAME}" \
        --query properties.configuration.ingress.fqdn \
        --output tsv)

    # Create Frontend Container App (0.25 vCPU, 0.5 GB memory - minimum)
    print_info "Creating Frontend Container App: ${FRONTEND_APP}"
    az containerapp create \
        --name "${FRONTEND_APP}" \
        --resource-group "${RG_NAME}" \
        --environment "${CONTAINER_ENV}" \
        --image "nginx:alpine" \
        --target-port 80 \
        --ingress external \
        --cpu 0.25 \
        --memory 0.5Gi \
        --min-replicas 0 \
        --max-replicas 2 \
        --scale-rule-name "http-scale" \
        --scale-rule-type "http" \
        --scale-rule-http-concurrency 20 \
        --env-vars "VITE_API_BASE=https://${BACKEND_URL}"

    # Get frontend URL
    FRONTEND_URL=$(az containerapp show \
        --name "${FRONTEND_APP}" \
        --resource-group "${RG_NAME}" \
        --query properties.configuration.ingress.fqdn \
        --output tsv)

    print_info "✅ ${ENV} environment created successfully!"
    print_info "Backend URL: https://${BACKEND_URL}"
    print_info "Frontend URL: https://${FRONTEND_URL}"
    echo ""
}

# Main script
print_info "Starting Azure infrastructure setup..."
print_info "Location: ${LOCATION}"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    print_error "Azure CLI is not installed. Please install it first."
    exit 1
fi

# Login check
if ! az account show &> /dev/null; then
    print_warning "Not logged in to Azure. Please login..."
    az login
fi

# Set subscription if provided
if [ -n "${SUBSCRIPTION_ID}" ]; then
    print_info "Setting subscription: ${SUBSCRIPTION_ID}"
    az account set --subscription "${SUBSCRIPTION_ID}"
fi

# Show current subscription
CURRENT_SUB=$(az account show --query name --output tsv)
print_info "Using subscription: ${CURRENT_SUB}"
echo ""

# Create Test environment
create_environment "test"

# Create Prod environment
create_environment "prod"

print_info "========================================="
print_info "Infrastructure setup complete!"
print_info "========================================="
print_info ""
print_info "Next steps:"
print_info "1. Configure connection strings and secrets:"
print_info "   chmod +x configure-secrets.sh"
print_info "   ./configure-secrets.sh test"
print_info "   ./configure-secrets.sh prod"
print_info ""
print_info "2. Set up GitHub secrets for deployment:"
print_info "   - Create a service principal:"
print_info "     az ad sp create-for-rbac --name 'whatismyfridge-github' --role contributor --scopes /subscriptions/YOUR_SUBSCRIPTION_ID --sdk-auth"
print_info "   - Add the JSON output as AZURE_CREDENTIALS secret in GitHub"
print_info ""
print_info "2. Make your GitHub Container Registry public or add credentials:"
print_info "   - Go to GitHub > Settings > Packages"
print_info "   - Or create a GitHub Personal Access Token with packages:read scope"
print_info ""
print_info "3. Build and push Docker images using GitHub Actions:"
print_info "   - Go to Actions > Build and Push Docker Images > Run workflow"
print_info ""
print_info "4. Deploy to Azure using GitHub Actions:"
print_info "   - Go to Actions > Deploy to Azure > Run workflow"
print_info ""
print_info "Estimated monthly cost (both environments, minimal usage):"
print_info "  - Container Apps: ~\$5-10/month (with scale-to-zero)"
print_info "  - Storage Account: ~\$0.50-1/month"
print_info "  - Log Analytics: ~\$2-5/month"
print_info "  - Total: ~\$15-20/month"
