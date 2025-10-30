@description('The base name for all resources')
param projectName string = 'whatsinmyfridge'

@description('The location for all resources')
param location string = resourceGroup().location

@description('The JWT secret key for authentication')
@secure()
param jwtSecretKey string

@description('Environment name (Production, Staging, Development)')
param environmentName string = 'Production'

@description('Custom domain for the API (leave empty for default)')
param apiCustomDomain string = ''

@description('Custom domain for the frontend (leave empty for default)')
param frontendCustomDomain string = ''

@description('GitHub repository URL for the frontend')
param githubRepoUrl string = ''

@description('GitHub repository branch')
param githubBranch string = 'main'

@description('Enable Blob Storage for photos (if false, uses local file storage)')
param enableBlobStorage bool = true

// Variables
var cosmosAccountName = toLower('${projectName}-cosmos')
var storageAccountName = toLower(replace('${projectName}storage', '-', ''))
var appServicePlanName = '${projectName}-plan'
var appServiceName = '${projectName}-api'
var staticWebAppName = '${projectName}-frontend'
var blobContainerName = 'recipe-photos'

// Cosmos DB Account (Free Tier)
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableFreeTier: true
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
  }
}

// Cosmos DB Database
resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-11-15' = {
  parent: cosmosAccount
  name: 'WhatIsInMyFridge'
  properties: {
    resource: {
      id: 'WhatIsInMyFridge'
    }
  }
}

// Cosmos DB Containers
resource usersContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: cosmosDatabase
  name: 'Users'
  properties: {
    resource: {
      id: 'Users'
      partitionKey: {
        paths: [
          '/Id'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
      }
    }
  }
}

resource householdsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: cosmosDatabase
  name: 'Households'
  properties: {
    resource: {
      id: 'Households'
      partitionKey: {
        paths: [
          '/Id'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource foodItemsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: cosmosDatabase
  name: 'FoodItems'
  properties: {
    resource: {
      id: 'FoodItems'
      partitionKey: {
        paths: [
          '/HouseholdId'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource recipesContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: cosmosDatabase
  name: 'Recipes'
  properties: {
    resource: {
      id: 'Recipes'
      partitionKey: {
        paths: [
          '/HouseholdId'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource recipeIngredientsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: cosmosDatabase
  name: 'RecipeIngredients'
  properties: {
    resource: {
      id: 'RecipeIngredients'
      partitionKey: {
        paths: [
          '/RecipeId'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource groceryItemsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: cosmosDatabase
  name: 'GroceryItems'
  properties: {
    resource: {
      id: 'GroceryItems'
      partitionKey: {
        paths: [
          '/HouseholdId'
        ]
        kind: 'Hash'
      }
    }
  }
}

// Storage Account for Blob Storage (optional)
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = if (enableBlobStorage) {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: true
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

// Blob Service
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = if (enableBlobStorage) {
  parent: storageAccount
  name: 'default'
}

// Blob Container for recipe photos
resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = if (enableBlobStorage) {
  parent: blobService
  name: blobContainerName
  properties: {
    publicAccess: 'Blob'
  }
}

// App Service Plan (F1 Free Tier)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
    size: 'F1'
    family: 'F'
    capacity: 1
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// App Service (Backend API)
resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appServiceName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET|9.0'
      alwaysOn: false // Not available on F1 tier
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName
        }
        {
          name: 'JWT_SECRET_KEY'
          value: jwtSecretKey
        }
        {
          name: 'COSMOS_CONNECTION_STRING'
          value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
        }
        {
          name: 'BLOB_STORAGE_CONNECTION_STRING'
          value: enableBlobStorage ? 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' : ''
        }
        {
          name: 'Jwt__Key'
          value: jwtSecretKey
        }
        {
          name: 'Jwt__Issuer'
          value: 'WhatIsInMyFridge.Api'
        }
        {
          name: 'Jwt__Audience'
          value: 'WhatIsInMyFridge.Client'
        }
        {
          name: 'CosmosDb__DatabaseName'
          value: 'WhatIsInMyFridge'
        }
        {
          name: 'BlobStorage__ContainerName'
          value: blobContainerName
        }
      ]
      cors: {
        allowedOrigins: [
          'http://localhost:5173'
          'https://${staticWebAppName}.azurestaticapps.net'
          apiCustomDomain != '' ? 'https://${apiCustomDomain}' : 'https://${appServiceName}.azurewebsites.net'
          frontendCustomDomain != '' ? 'https://${frontendCustomDomain}' : ''
        ]
        supportCredentials: true
      }
    }
  }
}

// Static Web App (Frontend)
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = if (githubRepoUrl != '') {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    repositoryUrl: githubRepoUrl
    branch: githubBranch
    buildProperties: {
      appLocation: 'frontend'
      outputLocation: 'dist'
      appBuildCommand: 'deno task build'
    }
  }
}

// Static Web App Settings
resource staticWebAppSettings 'Microsoft.Web/staticSites/config@2023-01-01' = if (githubRepoUrl != '') {
  parent: staticWebApp
  name: 'appsettings'
  properties: {
    VITE_API_BASE: apiCustomDomain != '' ? 'https://${apiCustomDomain}' : 'https://${appServiceName}.azurewebsites.net'
  }
}

// Outputs
output cosmosAccountName string = cosmosAccount.name
output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint
output storageBlobEndpoint string = enableBlobStorage ? storageAccount.properties.primaryEndpoints.blob : ''
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output staticWebAppUrl string = githubRepoUrl != '' ? 'https://${staticWebApp.properties.defaultHostname}' : ''
output resourceGroupName string = resourceGroup().name
output appServiceName string = appService.name
output staticWebAppName string = githubRepoUrl != '' ? staticWebApp.name : ''
