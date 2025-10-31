#!/bin/bash

# Complete configuration script for Azure Container Apps deployment
# Configures Cosmos DB, Blob Storage, secrets, and environment variables

set -e

# Configuration
ENV="${1:-test}"  # test or prod
LOCATION="eastus"

# Color codes
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
print_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }

# Resource names
RG="rg-whatismyfridge-${ENV}"
BACKEND_APP="ca-backend-${ENV}"
FRONTEND_APP="ca-frontend-${ENV}"
COSMOS_ACCOUNT="cosmos-whatismyfridge-${ENV}"
STORAGE_ACCOUNT="sawhatismyfridge${ENV}"

print_info "Configuring ${ENV} environment..."

# 1. Create Cosmos DB (serverless for cost optimization)
print_info "Creating Cosmos DB account..."
if ! az cosmosdb show --name "${COSMOS_ACCOUNT}" --resource-group "${RG}" &>/dev/null; then
  az cosmosdb create \
    --name "${COSMOS_ACCOUNT}" \
    --resource-group "${RG}" \
    --location "${LOCATION}" \
    --capabilities EnableServerless \
    --default-consistency-level Session
else
  print_info "Cosmos DB account already exists"
fi

# Create database
print_info "Creating database..."
az cosmosdb sql database create \
  --account-name "${COSMOS_ACCOUNT}" \
  --resource-group "${RG}" \
  --name "WhatIsInMyFridge" \
  --only-show-errors || true

# Create containers
print_info "Creating containers..."
declare -A containers=(
  ["Users"]="/id"
  ["Households"]="/id"
  ["FoodItems"]="/householdId"
  ["Recipes"]="/householdId"
  ["GroceryItems"]="/householdId"
)

for container in "${!containers[@]}"; do
  print_info "Creating container: ${container}"
  az cosmosdb sql container create \
    --account-name "${COSMOS_ACCOUNT}" \
    --resource-group "${RG}" \
    --database-name "WhatIsInMyFridge" \
    --name "${container}" \
    --partition-key-path "${containers[$container]}" \
    --only-show-errors || true
done

# 2. Create Blob Storage container
print_info "Creating Blob Storage container for photos..."
az storage container create \
  --name "recipe-photos" \
  --account-name "${STORAGE_ACCOUNT}" \
  --only-show-errors || true

# 3. Get connection strings
print_info "Retrieving connection strings..."
COSMOS_CONN=$(az cosmosdb keys list \
  --name "${COSMOS_ACCOUNT}" \
  --resource-group "${RG}" \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" \
  --output tsv)

BLOB_CONN=$(az storage account show-connection-string \
  --name "${STORAGE_ACCOUNT}" \
  --resource-group "${RG}" \
  --query connectionString \
  --output tsv)

JWT_SECRET=$(openssl rand -base64 64 | tr -d '\n')

# 4. Set backend secrets
print_info "Setting backend secrets..."
az containerapp secret set \
  --name "${BACKEND_APP}" \
  --resource-group "${RG}" \
  --secrets \
    cosmos-connection-string="${COSMOS_CONN}" \
    blob-storage-connection-string="${BLOB_CONN}" \
    jwt-secret-key="${JWT_SECRET}"

# 5. Configure backend environment variables
print_info "Configuring backend environment variables..."
FRONTEND_URL=$(az containerapp show \
  --name "${FRONTEND_APP}" \
  --resource-group "${RG}" \
  --query properties.configuration.ingress.fqdn \
  --output tsv)

ASP_ENV="Development"
if [ "${ENV}" = "prod" ]; then
  ASP_ENV="Production"
fi

az containerapp update \
  --name "${BACKEND_APP}" \
  --resource-group "${RG}" \
  --set-env-vars \
    ASPNETCORE_ENVIRONMENT="${ASP_ENV}" \
    COSMOS_CONNECTION_STRING=secretref:cosmos-connection-string \
    BLOB_STORAGE_CONNECTION_STRING=secretref:blob-storage-connection-string \
    JWT_SECRET_KEY=secretref:jwt-secret-key \
    "ALLOWED_ORIGINS=https://${FRONTEND_URL}"

# 6. Configure frontend
print_info "Configuring frontend environment variables..."
BACKEND_URL=$(az containerapp show \
  --name "${BACKEND_APP}" \
  --resource-group "${RG}" \
  --query properties.configuration.ingress.fqdn \
  --output tsv)

az containerapp update \
  --name "${FRONTEND_APP}" \
  --resource-group "${RG}" \
  --set-env-vars "VITE_API_BASE=https://${BACKEND_URL}"

print_info "========================================="
print_info "Configuration complete!"
print_info "========================================="
print_info "Frontend: https://${FRONTEND_URL}"
print_info "Backend:  https://${BACKEND_URL}"
print_info ""
print_info "Test your deployment:"
print_info "  curl https://${BACKEND_URL}/health"
print_info "  curl -I https://${FRONTEND_URL}"
