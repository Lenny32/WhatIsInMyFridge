#!/bin/bash

#############################################################################
# WhatIsInMyFridge - Fully Automated Azure Deployment
#############################################################################
# This script will:
# 1. Create all Azure resources (100% FREE TIER)
# 2. Build and deploy your backend API
# 3. Build and deploy your frontend
# 4. Configure everything automatically
#
# COST: $0/month using Azure free tiers
#############################################################################

set -e  # Exit on error

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

# Configuration
RESOURCE_GROUP_NAME="${RESOURCE_GROUP_NAME:-whatsinmyfridge-rg}"
LOCATION="${LOCATION:-eastus}"
PROJECT_NAME="${PROJECT_NAME:-whatsinmyfridge}"
BICEP_FILE="main.bicep"
PARAMETERS_FILE="main.parameters.json"

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

#############################################################################
# Helper Functions
#############################################################################

print_header() {
    echo -e "\n${BOLD}${BLUE}═══════════════════════════════════════════════════════${NC}"
    echo -e "${BOLD}${BLUE}  $1${NC}"
    echo -e "${BOLD}${BLUE}═══════════════════════════════════════════════════════${NC}\n"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ ERROR: $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ WARNING: $1${NC}"
}

print_info() {
    echo -e "${CYAN}ℹ $1${NC}"
}

print_step() {
    echo -e "\n${BOLD}${CYAN}▶ $1${NC}"
}

confirm_deployment() {
    print_header "DEPLOYMENT CONFIRMATION"
    
    echo -e "${BOLD}This script will deploy WhatIsInMyFridge to Azure${NC}"
    echo ""
    echo -e "${BOLD}Resources to be created:${NC}"
    echo "  • Resource Group: $RESOURCE_GROUP_NAME"
    echo "  • Location: $LOCATION"
    echo "  • Cosmos DB Account (FREE TIER - serverless)"
    echo "  • Storage Account (pay-as-you-go, ~\$0.02/month)"
    echo "  • App Service Plan (F1 FREE TIER)"
    echo "  • App Service (.NET 9 API)"
    echo "  • Static Web App (FREE TIER)"
    echo ""
    echo -e "${BOLD}${GREEN}ESTIMATED COST: \$0-2/month${NC}"
    echo ""
    echo -e "${BOLD}Cost Breakdown:${NC}"
    echo "  • Cosmos DB Serverless: ${GREEN}\$0/month${NC} (1000 RU/s + 25GB free forever)"
    echo "  • App Service F1: ${GREEN}\$0/month${NC} (60 CPU minutes/day limit)"
    echo "  • Static Web App: ${GREEN}\$0/month${NC} (100GB bandwidth/month)"
    echo "  • Blob Storage: ${YELLOW}~\$0-2/month${NC} (first 5GB free, then \$0.0184/GB)"
    echo ""
    echo -e "${YELLOW}${BOLD}⚠ IMPORTANT:${NC}"
    echo "  • Cosmos DB FREE TIER is limited to ${BOLD}ONE per Azure subscription${NC}"
    echo "  • If you already have a Cosmos DB free tier, this will fail"
    echo "  • You can disable free tier in parameters (will incur costs)"
    echo ""
    
    read -p "$(echo -e ${BOLD}Do you want to proceed? [y/N]: ${NC})" -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        print_warning "Deployment cancelled by user"
        exit 0
    fi
    print_success "Deployment confirmed"
}

check_prerequisites() {
    print_header "Checking Prerequisites"
    
    # Check Azure CLI
    if ! command -v az &> /dev/null; then
        print_error "Azure CLI is not installed"
        echo "Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
        exit 1
    fi
    print_success "Azure CLI installed: $(az version --query '\"azure-cli\"' -o tsv)"
    
    # Check Azure login
    if ! az account show &> /dev/null; then
        print_error "Not logged in to Azure"
        print_info "Running 'az login' for you..."
        az login
    fi
    
    SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
    SUBSCRIPTION_ID=$(az account show --query id -o tsv)
    print_success "Logged in to Azure"
    print_info "Subscription: $SUBSCRIPTION_NAME"
    print_info "Subscription ID: $SUBSCRIPTION_ID"
    
    # Check .NET SDK
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET SDK is not installed"
        echo "Install from: https://dotnet.microsoft.com/download/dotnet/9.0"
        exit 1
    fi
    print_success ".NET SDK installed: $(dotnet --version)"
    
    # Check Deno
    if ! command -v deno &> /dev/null; then
        print_error "Deno is not installed"
        echo "Install from: https://deno.land/"
        exit 1
    fi
    print_success "Deno installed: $(deno --version | head -n 1)"
    
    # Check jq (for JSON parsing)
    if ! command -v jq &> /dev/null; then
        print_warning "jq is not installed (recommended for output parsing)"
        print_info "Install: brew install jq (macOS) or apt-get install jq (Linux)"
    else
        print_success "jq installed"
    fi
    
    # Check if we're in the deploy directory
    if [ ! -f "$BICEP_FILE" ]; then
        print_error "Bicep file not found. Please run this script from the deploy/ directory"
        exit 1
    fi
    print_success "Bicep template found"
}

generate_jwt_secret() {
    print_header "JWT Secret Configuration"
    
    # Check if already configured
    if ! grep -q "REPLACE_WITH_YOUR_JWT_SECRET" "$PARAMETERS_FILE" 2>/dev/null; then
        print_success "JWT secret already configured"
        return
    fi
    
    print_info "Generating secure JWT secret..."
    JWT_SECRET=$(openssl rand -base64 32)
    
    # Update parameters file
    if [[ "$OSTYPE" == "darwin"* ]]; then
        sed -i '' "s/REPLACE_WITH_YOUR_JWT_SECRET/$JWT_SECRET/" "$PARAMETERS_FILE"
    else
        sed -i "s/REPLACE_WITH_YOUR_JWT_SECRET/$JWT_SECRET/" "$PARAMETERS_FILE"
    fi
    
    print_success "Generated and configured JWT secret"
}

create_resource_group() {
    print_header "Creating Resource Group"
    
    if az group show --name "$RESOURCE_GROUP_NAME" &> /dev/null; then
        print_warning "Resource group '$RESOURCE_GROUP_NAME' already exists"
        print_info "Will update existing resources"
    else
        print_step "Creating resource group '$RESOURCE_GROUP_NAME' in $LOCATION..."
        az group create \
            --name "$RESOURCE_GROUP_NAME" \
            --location "$LOCATION" \
            --output none
        print_success "Resource group created"
    fi
}

deploy_infrastructure() {
    print_header "Deploying Azure Infrastructure"
    
    print_step "Validating Bicep template..."
    az deployment group validate \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --template-file "$BICEP_FILE" \
        --parameters "@$PARAMETERS_FILE" \
        --output none
    print_success "Template validation passed"
    
    print_step "Deploying infrastructure (this takes 5-10 minutes)..."
    print_info "Provisioning: Cosmos DB, Storage Account, App Service, Static Web App..."
    
    az deployment group create \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --template-file "$BICEP_FILE" \
        --parameters "@$PARAMETERS_FILE" \
        --output json > "$SCRIPT_DIR/deployment-output.json"
    
    print_success "Infrastructure deployed successfully!"
}

build_backend() {
    print_header "Building Backend API"
    
    print_step "Building .NET 9 API..."
    cd "$PROJECT_ROOT/backend/WhatIsInMyFridge.Api"
    
    # Clean previous builds
    rm -rf ./bin ./obj ./publish 2>/dev/null || true
    
    # Build for production
    dotnet publish -c Release -o ./publish --nologo
    
    print_success "Backend built successfully"
}

deploy_backend() {
    print_header "Deploying Backend to Azure App Service"
    
    cd "$PROJECT_ROOT/backend/WhatIsInMyFridge.Api/publish"
    
    print_step "Creating deployment package..."
    rm -f ../deploy.zip
    zip -r ../deploy.zip . > /dev/null
    print_success "Deployment package created"
    
    print_step "Deploying to App Service..."
    APP_SERVICE_NAME=$(jq -r '.properties.outputs.appServiceName.value' "$SCRIPT_DIR/deployment-output.json" 2>/dev/null || echo "$PROJECT_NAME-api")
    
    az webapp deploy \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --name "$APP_SERVICE_NAME" \
        --src-path "../deploy.zip" \
        --type zip \
        --async true
    
    print_success "Backend deployed to App Service"
    print_info "App Service will start automatically in ~30-60 seconds"
}

build_frontend() {
    print_header "Building Frontend"
    
    print_step "Building Svelte frontend..."
    cd "$PROJECT_ROOT/frontend"
    
    # Get API URL from deployment
    API_URL=$(jq -r '.properties.outputs.appServiceUrl.value' "$SCRIPT_DIR/deployment-output.json" 2>/dev/null)
    
    if [ -n "$API_URL" ] && [ "$API_URL" != "null" ]; then
        # Create .env for build
        echo "VITE_API_BASE=$API_URL" > .env
        print_info "Frontend configured to use API: $API_URL"
    fi
    
    # Clean previous builds
    rm -rf ./dist 2>/dev/null || true
    
    # Build
    deno task build
    
    print_success "Frontend built successfully"
}

deploy_frontend() {
    print_header "Deploying Frontend to Azure Static Web Apps"
    
    STATIC_WEB_APP_NAME=$(jq -r '.properties.outputs.staticWebAppName.value' "$SCRIPT_DIR/deployment-output.json" 2>/dev/null)
    
    if [ -z "$STATIC_WEB_APP_NAME" ] || [ "$STATIC_WEB_APP_NAME" == "null" ] || [ "$STATIC_WEB_APP_NAME" == "" ]; then
        print_warning "Static Web App not deployed (GitHub URL not configured)"
        print_info "Your frontend is built in: $PROJECT_ROOT/frontend/dist"
        print_info ""
        print_info "To deploy manually:"
        print_info "1. Create a Static Web App in Azure Portal"
        print_info "2. Upload the contents of frontend/dist/"
        print_info "OR configure GitHub URL in parameters and re-run deployment"
        return
    fi
    
    print_step "Getting Static Web App deployment token..."
    DEPLOYMENT_TOKEN=$(az staticwebapp secrets list \
        --name "$STATIC_WEB_APP_NAME" \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --query "properties.apiKey" -o tsv)
    
    if [ -z "$DEPLOYMENT_TOKEN" ]; then
        print_warning "Could not get deployment token"
        print_info "Frontend will be deployed automatically via GitHub Actions"
        print_info "Check your GitHub repository for deployment status"
        return
    fi
    
    print_step "Deploying frontend to Static Web App..."
    cd "$PROJECT_ROOT/frontend"
    
    # Install SWA CLI if not present
    if ! command -v swa &> /dev/null; then
        print_info "Installing Azure Static Web Apps CLI..."
        npm install -g @azure/static-web-apps-cli
    fi
    
    # Deploy using SWA CLI
    swa deploy ./dist \
        --deployment-token "$DEPLOYMENT_TOKEN" \
        --env production
    
    print_success "Frontend deployed to Static Web App"
}

display_outputs() {
    print_header "🎉 DEPLOYMENT COMPLETE!"
    
    if [ -f "$SCRIPT_DIR/deployment-output.json" ] && command -v jq &> /dev/null; then
        API_URL=$(jq -r '.properties.outputs.appServiceUrl.value' "$SCRIPT_DIR/deployment-output.json")
        FRONTEND_URL=$(jq -r '.properties.outputs.staticWebAppUrl.value' "$SCRIPT_DIR/deployment-output.json")
        COSMOS_ENDPOINT=$(jq -r '.properties.outputs.cosmosEndpoint.value' "$SCRIPT_DIR/deployment-output.json")
        
        echo -e "${BOLD}Your application is now live!${NC}\n"
        
        echo -e "${BOLD}${GREEN}🌐 Frontend URL:${NC}"
        if [ -n "$FRONTEND_URL" ] && [ "$FRONTEND_URL" != "null" ] && [ "$FRONTEND_URL" != "" ]; then
            echo -e "   ${CYAN}$FRONTEND_URL${NC}"
        else
            echo -e "   ${YELLOW}Not deployed (configure GitHub URL to enable)${NC}"
        fi
        
        echo -e "\n${BOLD}${GREEN}🔧 Backend API URL:${NC}"
        echo -e "   ${CYAN}$API_URL${NC}"
        
        echo -e "\n${BOLD}${GREEN}🗄️  Cosmos DB Endpoint:${NC}"
        echo -e "   ${CYAN}$COSMOS_ENDPOINT${NC}"
        
        echo -e "\n${BOLD}${GREEN}📦 Resource Group:${NC}"
        echo -e "   ${CYAN}$RESOURCE_GROUP_NAME${NC}"
        
        echo -e "\n${BOLD}Next Steps:${NC}"
        echo "  1. Wait ~1-2 minutes for services to fully start"
        echo "  2. Visit your frontend URL to start using the app"
        echo "  3. Create your first user account via the Register page"
        echo ""
        echo -e "${BOLD}Monitoring:${NC}"
        echo "  • Azure Portal: https://portal.azure.com"
        echo "  • View logs: az webapp log tail --name $APP_SERVICE_NAME --resource-group $RESOURCE_GROUP_NAME"
        echo ""
        echo -e "${BOLD}Manage:${NC}"
        echo "  • Update backend: Re-run this script"
        echo "  • View costs: Azure Portal > Cost Management"
        echo "  • Delete everything: az group delete --name $RESOURCE_GROUP_NAME --yes"
    else
        print_info "Deployment outputs saved to: $SCRIPT_DIR/deployment-output.json"
    fi
    
    echo ""
}

cleanup() {
    print_step "Cleaning up temporary files..."
    cd "$SCRIPT_DIR"
    rm -f deployment-output.json 2>/dev/null || true
    rm -f "$PROJECT_ROOT/backend/WhatIsInMyFridge.Api/deploy.zip" 2>/dev/null || true
    rm -f "$PROJECT_ROOT/frontend/.env" 2>/dev/null || true
    print_success "Cleanup complete"
}

#############################################################################
# Main Deployment Flow
#############################################################################

main() {
    print_header "WhatIsInMyFridge - Automated Azure Deployment"
    
    cd "$SCRIPT_DIR"
    
    # Pre-flight checks
    check_prerequisites
    
    # Get user confirmation
    confirm_deployment
    
    # Generate secrets
    generate_jwt_secret
    
    # Deploy infrastructure
    create_resource_group
    deploy_infrastructure
    
    # Build and deploy backend
    build_backend
    deploy_backend
    
    # Build and deploy frontend
    build_frontend
    deploy_frontend
    
    # Show results
    display_outputs
    
    # Cleanup
    # cleanup  # Keep files for debugging
    
    print_success "All done! 🚀"
}

# Handle script interruption
trap 'print_error "Deployment interrupted"; exit 1' INT TERM

# Run main function
main "$@"
