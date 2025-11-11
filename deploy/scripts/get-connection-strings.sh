#!/bin/bash

# Script to retrieve connection strings from Azure Container Apps
# Usage: ./get-connection-strings.sh <environment>
# Example: ./get-connection-strings.sh test

set -e

ENVIRONMENT=${1:-test}

if [[ "$ENVIRONMENT" != "test" && "$ENVIRONMENT" != "prod" ]]; then
    echo "Error: Environment must be 'test' or 'prod'"
    exit 1
fi

RESOURCE_GROUP="rg-whatismyfridge-${ENVIRONMENT}"
BACKEND_APP="ca-backend-${ENVIRONMENT}"

echo "===================================="
echo "Getting connection strings for: $ENVIRONMENT"
echo "Resource Group: $RESOURCE_GROUP"
echo "Backend App: $BACKEND_APP"
echo "===================================="
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "Error: Azure CLI is not installed"
    exit 1
fi

# Check if logged in
if ! az account show &> /dev/null; then
    echo "Error: Not logged in to Azure CLI. Run 'az login' first."
    exit 1
fi

echo "📦 Getting Cosmos DB Connection String..."
COSMOS_CONNECTION=$(az containerapp secret show \
  --name "$BACKEND_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --secret-name cosmos-connection-string \
  --query value -o tsv 2>/dev/null) || echo "Failed to retrieve"

echo "Cosmos DB Connection String:"
echo "$COSMOS_CONNECTION"
echo ""

echo "📦 Getting Blob Storage Connection String..."
BLOB_CONNECTION=$(az containerapp secret show \
  --name "$BACKEND_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --secret-name blob-storage-connection-string \
  --query value -o tsv 2>/dev/null) || echo "Failed to retrieve"

echo "Blob Storage Connection String:"
echo "$BLOB_CONNECTION"
echo ""

echo "📦 Getting JWT Secret Key..."
JWT_KEY=$(az containerapp secret show \
  --name "$BACKEND_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --secret-name jwt-secret-key \
  --query value -o tsv 2>/dev/null) || echo "Failed to retrieve"

echo "JWT Secret Key:"
echo "$JWT_KEY"
echo ""

echo "===================================="
echo "✅ Done!"
echo "===================================="
