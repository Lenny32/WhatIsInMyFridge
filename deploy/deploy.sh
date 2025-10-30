#!/bin/bash

# Azure Bicep Deployment Script for WhatIsInMyFridge
# This script deploys the entire infrastructure to Azure using Bicep templates

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
RESOURCE_GROUP_NAME="${RESOURCE_GROUP_NAME:-whatsinmyfridge-rg}"
LOCATION="${LOCATION:-eastus}"
BICEP_FILE="main.bicep"
PARAMETERS_FILE="main.parameters.json"

# Helper functions
print_header() {
    echo -e "\n${BLUE}===================================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}===================================================${NC}\n"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

# Check prerequisites
check_prerequisites() {
    print_header "Checking Prerequisites"
    
    # Check if Azure CLI is installed
    if ! command -v az &> /dev/null; then
        print_error "Azure CLI is not installed. Please install it from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
        exit 1
    fi
    print_success "Azure CLI is installed"
    
    # Check if logged in to Azure
    if ! az account show &> /dev/null; then
        print_error "Not logged in to Azure. Please run 'az login' first"
        exit 1
    fi
    print_success "Logged in to Azure"
    
    # Display current subscription
    SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
    SUBSCRIPTION_ID=$(az account show --query id -o tsv)
    print_info "Current subscription: $SUBSCRIPTION_NAME ($SUBSCRIPTION_ID)"
    
    # Check if Bicep file exists
    if [ ! -f "$BICEP_FILE" ]; then
        print_error "Bicep file not found: $BICEP_FILE"
        exit 1
    fi
    print_success "Bicep template found"
    
    # Check if parameters file exists
    if [ ! -f "$PARAMETERS_FILE" ]; then
        print_error "Parameters file not found: $PARAMETERS_FILE"
        exit 1
    fi
    print_success "Parameters file found"
}

# Generate JWT secret if needed
generate_jwt_secret() {
    print_header "JWT Secret Configuration"
    
    # Check if JWT secret is set in parameters file
    JWT_SECRET=$(grep -o '"REPLACE_WITH_YOUR_JWT_SECRET"' "$PARAMETERS_FILE" || true)
    
    if [ ! -z "$JWT_SECRET" ]; then
        print_warning "JWT secret needs to be configured"
        
        # Generate a random JWT secret
        GENERATED_SECRET=$(openssl rand -base64 32)
        
        echo -e "\nGenerated JWT secret: ${GREEN}$GENERATED_SECRET${NC}"
        echo -e "\nOptions:"
        echo "1. Use the generated secret (recommended)"
        echo "2. Enter your own secret"
        echo "3. Set it manually later in Azure Portal"
        
        read -p "Choose an option (1-3): " choice
        
        case $choice in
            1)
                # Update parameters file with generated secret
                if [[ "$OSTYPE" == "darwin"* ]]; then
                    sed -i '' "s/REPLACE_WITH_YOUR_JWT_SECRET/$GENERATED_SECRET/" "$PARAMETERS_FILE"
                else
                    sed -i "s/REPLACE_WITH_YOUR_JWT_SECRET/$GENERATED_SECRET/" "$PARAMETERS_FILE"
                fi
                print_success "Parameters file updated with generated JWT secret"
                ;;
            2)
                read -sp "Enter your JWT secret: " USER_SECRET
                echo
                if [[ "$OSTYPE" == "darwin"* ]]; then
                    sed -i '' "s/REPLACE_WITH_YOUR_JWT_SECRET/$USER_SECRET/" "$PARAMETERS_FILE"
                else
                    sed -i "s/REPLACE_WITH_YOUR_JWT_SECRET/$USER_SECRET/" "$PARAMETERS_FILE"
                fi
                print_success "Parameters file updated with your JWT secret"
                ;;
            3)
                print_warning "You will need to set the JWT secret manually in Azure Portal after deployment"
                print_info "Navigate to: App Service > Configuration > Application settings > JWT_SECRET_KEY"
                ;;
            *)
                print_error "Invalid option"
                exit 1
                ;;
        esac
    else
        print_success "JWT secret is already configured"
    fi
}

# Create resource group
create_resource_group() {
    print_header "Creating Resource Group"
    
    if az group show --name "$RESOURCE_GROUP_NAME" &> /dev/null; then
        print_warning "Resource group '$RESOURCE_GROUP_NAME' already exists"
        read -p "Do you want to continue and update existing resources? (y/n): " confirm
        if [ "$confirm" != "y" ]; then
            print_info "Deployment cancelled"
            exit 0
        fi
    else
        az group create \
            --name "$RESOURCE_GROUP_NAME" \
            --location "$LOCATION" \
            --output none
        print_success "Resource group '$RESOURCE_GROUP_NAME' created in $LOCATION"
    fi
}

# Validate Bicep template
validate_template() {
    print_header "Validating Bicep Template"
    
    az deployment group validate \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --template-file "$BICEP_FILE" \
        --parameters "@$PARAMETERS_FILE" \
        --output none
    
    print_success "Bicep template validation passed"
}

# Deploy infrastructure
deploy_infrastructure() {
    print_header "Deploying Infrastructure"
    
    print_info "This may take 5-10 minutes..."
    
    az deployment group create \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --template-file "$BICEP_FILE" \
        --parameters "@$PARAMETERS_FILE" \
        --output json > deployment-output.json
    
    print_success "Infrastructure deployed successfully"
}

# Display deployment outputs
display_outputs() {
    print_header "Deployment Outputs"
    
    APP_SERVICE_URL=$(jq -r '.properties.outputs.appServiceUrl.value' deployment-output.json)
    STATIC_WEB_APP_URL=$(jq -r '.properties.outputs.staticWebAppUrl.value' deployment-output.json)
    COSMOS_ENDPOINT=$(jq -r '.properties.outputs.cosmosEndpoint.value' deployment-output.json)
    BLOB_ENDPOINT=$(jq -r '.properties.outputs.storageBlobEndpoint.value' deployment-output.json)
    APP_SERVICE_NAME=$(jq -r '.properties.outputs.appServiceName.value' deployment-output.json)
    STATIC_WEB_APP_NAME=$(jq -r '.properties.outputs.staticWebAppName.value' deployment-output.json)
    
    echo -e "${GREEN}Backend API:${NC} $APP_SERVICE_URL"
    
    if [ "$STATIC_WEB_APP_URL" != "" ] && [ "$STATIC_WEB_APP_URL" != "null" ]; then
        echo -e "${GREEN}Frontend:${NC} $STATIC_WEB_APP_URL"
    else
        print_warning "Static Web App not deployed (GitHub repo URL not configured)"
    fi
    
    echo -e "${GREEN}Cosmos DB Endpoint:${NC} $COSMOS_ENDPOINT"
    
    if [ "$BLOB_ENDPOINT" != "" ] && [ "$BLOB_ENDPOINT" != "null" ]; then
        echo -e "${GREEN}Blob Storage Endpoint:${NC} $BLOB_ENDPOINT"
    fi
    
    echo ""
}

# Display next steps
display_next_steps() {
    print_header "Next Steps"
    
    APP_SERVICE_NAME=$(jq -r '.properties.outputs.appServiceName.value' deployment-output.json)
    STATIC_WEB_APP_NAME=$(jq -r '.properties.outputs.staticWebAppName.value' deployment-output.json)
    
    echo "1. Deploy the backend API:"
    echo -e "   ${YELLOW}cd ../backend/WhatIsInMyFridge.Api${NC}"
    echo -e "   ${YELLOW}dotnet publish -c Release -o ./publish${NC}"
    echo -e "   ${YELLOW}cd publish && zip -r ../deploy.zip .${NC}"
    echo -e "   ${YELLOW}az webapp deploy --resource-group $RESOURCE_GROUP_NAME --name $APP_SERVICE_NAME --src-path ../deploy.zip${NC}"
    echo ""
    
    if [ "$STATIC_WEB_APP_NAME" != "" ] && [ "$STATIC_WEB_APP_NAME" != "null" ]; then
        echo "2. Frontend will be automatically deployed via GitHub Actions"
        echo -e "   View deployment status: ${YELLOW}https://portal.azure.com${NC}"
    else
        echo "2. Deploy the frontend manually or configure GitHub integration:"
        echo -e "   ${YELLOW}cd ../frontend${NC}"
        echo -e "   ${YELLOW}deno task build${NC}"
        echo -e "   ${YELLOW}az staticwebapp create ... (or configure in Azure Portal)${NC}"
    fi
    echo ""
    
    echo "3. Configure custom domains (optional):"
    echo -e "   Backend:  ${YELLOW}az webapp config hostname add ...${NC}"
    echo -e "   Frontend: ${YELLOW}az staticwebapp hostname set ...${NC}"
    echo ""
    
    echo "4. Monitor your application:"
    echo -e "   ${YELLOW}https://portal.azure.com${NC}"
    echo ""
    
    print_success "Deployment complete!"
}

# Main deployment flow
main() {
    print_header "WhatIsInMyFridge - Azure Deployment"
    
    check_prerequisites
    generate_jwt_secret
    create_resource_group
    validate_template
    deploy_infrastructure
    display_outputs
    display_next_steps
    
    # Clean up
    rm -f deployment-output.json
}

# Run main function
main
