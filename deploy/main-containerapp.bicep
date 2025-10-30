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
var containerAppEnvName = '${projectName}-env'
var containerAppName = '${projectName}-api'
var staticWebAppName = '${projectName}-frontend'
var blobContainerName = 'recipe-photos'
var logAnalyticsName = '${projectName}-logs'

// Log Analytics Workspace (required for Container Apps)
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Cosmos DB Account (Free Tier) - Already exists, reference it
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' existing = {
  name: cosmosAccountName
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

// Storage Account for Blob Storage - Already exists, reference it
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = if (enableBlobStorage) {
  name: storageAccountName
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

// Container Apps Environment
resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// Container App (Backend API)
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
        corsPolicy: {
          allowedOrigins: [
            'http://localhost:5173'
            'https://${staticWebAppName}.azurestaticapps.net'
            apiCustomDomain != '' ? 'https://${apiCustomDomain}' : ''
            frontendCustomDomain != '' ? 'https://${frontendCustomDomain}' : ''
          ]
          allowedMethods: [
            'GET'
            'POST'
            'PUT'
            'DELETE'
            'OPTIONS'
          ]
          allowedHeaders: [
            '*'
          ]
          allowCredentials: true
        }
      }
      secrets: [
        {
          name: 'jwt-secret'
          value: jwtSecretKey
        }
        {
          name: 'cosmos-connection'
          value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
        }
        {
          name: 'blob-connection'
          value: enableBlobStorage ? 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' : ''
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: 'mcr.microsoft.com/dotnet/samples:aspnetapp' // Placeholder - will be updated during deployment
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environmentName
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'JWT_SECRET_KEY'
              secretRef: 'jwt-secret'
            }
            {
              name: 'COSMOS_CONNECTION_STRING'
              secretRef: 'cosmos-connection'
            }
            {
              name: 'BLOB_STORAGE_CONNECTION_STRING'
              secretRef: 'blob-connection'
            }
            {
              name: 'Jwt__Key'
              secretRef: 'jwt-secret'
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
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
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
    VITE_API_BASE: apiCustomDomain != '' ? 'https://${apiCustomDomain}' : 'https://${containerApp.properties.configuration.ingress.fqdn}'
  }
}

// Outputs
output cosmosAccountName string = cosmosAccount.name
output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint
output storageBlobEndpoint string = enableBlobStorage ? storageAccount.properties.primaryEndpoints.blob : ''
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output staticWebAppUrl string = githubRepoUrl != '' ? 'https://${staticWebApp.properties.defaultHostname}' : ''
output resourceGroupName string = resourceGroup().name
output containerAppName string = containerApp.name
output staticWebAppName string = githubRepoUrl != '' ? staticWebApp.name : ''
