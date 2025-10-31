@description('Environment name (test, test2, or prod)')
@allowed([
  'test'
  'test2'
  'prod'
])
param environment string

@description('Location for all resources')
param location string = 'germanywestcentral'

@description('Location for Log Analytics (must be a supported region)')
param logAnalyticsLocation string = 'germanywestcentral'

@description('Cosmos DB account name')
param cosmosAccountName string = 'cosmos-${uniqueString(resourceGroup().id)}-${environment}'

@description('Cosmos DB database name')
param cosmosDatabaseName string = 'WhatIsInMyFridge'

@description('Storage account name for blobs')
param storageAccountName string = 'st${uniqueString(resourceGroup().id)}${environment}'

@description('Container registry server')
param containerRegistryServer string = 'ghcr.io'

@description('Container registry username')
param containerRegistryUsername string

@description('Container registry password')
@secure()
param containerRegistryPassword string

@description('Frontend container image')
param frontendImage string

@description('Backend container image')
param backendImage string

@description('JWT secret key')
@secure()
param jwtSecretKey string

@description('Optional revision suffix to force new revision creation')
param revisionSuffix string = ''

var resourceGroupName = 'rg-whatismyfridge-${environment}'
var containerAppEnvName = 'cae-whatismyfridge-${environment}'
var frontendAppName = 'ca-frontend-${environment}'
var backendAppName = 'ca-backend-${environment}'
var logAnalyticsName = 'log-whatismyfridge-${environment}'
var appInsightsName = 'ai-whatismyfridge-${environment}'
var blobContainerName = 'recipe-photos'
var frontendFqdn = '${frontendAppName}.${containerAppEnv.properties.defaultDomain}'
var backendFqdn = '${backendAppName}.${containerAppEnv.properties.defaultDomain}'

// Log Analytics Workspace - Free tier with 5GB/month (in Germany)
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: logAnalyticsLocation
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30 // Minimum retention for PerGB2018 SKU
    workspaceCapping: {
      dailyQuotaGb: json('0.16') // ~5GB per month (free tier limit)
    }
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// Storage Account for Blob Storage - Cheapest configuration
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS' // Cheapest redundancy option
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Cool' // Cheaper storage for infrequently accessed data
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

// Blob Service
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

// Blob Container for recipe photos
resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: blobContainerName
  properties: {
    publicAccess: 'None'
  }
}

// Cosmos DB Account
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
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
resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: cosmosDatabaseName
  properties: {
    resource: {
      id: cosmosDatabaseName
    }
  }
}

// Cosmos DB Containers
resource usersContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'Users'
  properties: {
    resource: {
      id: 'Users'
      partitionKey: {
        paths: [
          '/id'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource householdsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'Households'
  properties: {
    resource: {
      id: 'Households'
      partitionKey: {
        paths: [
          '/id'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource foodItemsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'FoodItems'
  properties: {
    resource: {
      id: 'FoodItems'
      partitionKey: {
        paths: [
          '/householdId'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource recipesContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'Recipes'
  properties: {
    resource: {
      id: 'Recipes'
      partitionKey: {
        paths: [
          '/householdId'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource groceryItemsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'GroceryItems'
  properties: {
    resource: {
      id: 'GroceryItems'
      partitionKey: {
        paths: [
          '/householdId'
        ]
        kind: 'Hash'
      }
    }
  }
}

// Container Apps Environment
resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
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

// Backend Container App
resource backendApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: backendAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
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
            'https://${frontendFqdn}'
          ]
          allowedMethods: [
            'GET'
            'POST'
            'PUT'
            'PATCH'
            'DELETE'
            'OPTIONS'
          ]
          allowedHeaders: [
            '*'
          ]
          allowCredentials: true
        }
      }
      registries: [
        {
          server: containerRegistryServer
          username: containerRegistryUsername
          passwordSecretRef: 'container-registry-password'
        }
      ]
      secrets: [
        {
          name: 'container-registry-password'
          value: containerRegistryPassword
        }
        {
          name: 'cosmos-connection-string'
          value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
        }
        {
          name: 'blob-storage-connection-string'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'jwt-secret-key'
          value: jwtSecretKey
        }
      ]
    }
    template: {
      revisionSuffix: revisionSuffix != '' ? revisionSuffix : null
      containers: [
        {
          name: 'backend'
          image: backendImage
          resources: {
            cpu: json('0.25') // Minimum CPU for lowest cost
            memory: '0.5Gi'   // Minimum memory for lowest cost
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environment == 'prod' ? 'Production' : 'Development'
            }
            {
              name: 'COSMOS_CONNECTION_STRING'
              secretRef: 'cosmos-connection-string'
            }
            {
              name: 'COSMOS_DATABASE_NAME'
              value: cosmosDatabaseName
            }
            {
              name: 'BLOB_STORAGE_CONNECTION_STRING'
              secretRef: 'blob-storage-connection-string'
            }
            {
              name: 'JWT_SECRET_KEY'
              secretRef: 'jwt-secret-key'
            }
            {
              name: 'Jwt__Issuer'
              value: 'WhatIsInMyFridge'
            }
            {
              name: 'Jwt__Audience'
              value: 'WhatIsInMyFridge'
            }
            {
              name: 'ALLOWED_ORIGINS'
              value: 'https://${frontendFqdn}'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsights.properties.ConnectionString
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0 // Scale to 0 for all environments to minimize cost
        maxReplicas: 1 // Limit to 1 replica maximum
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

// Frontend Container App
resource frontendApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: frontendAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 80
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: containerRegistryServer
          username: containerRegistryUsername
          passwordSecretRef: 'container-registry-password'
        }
      ]
      secrets: [
        {
          name: 'container-registry-password'
          value: containerRegistryPassword
        }
      ]
    }
    template: {
      revisionSuffix: revisionSuffix != '' ? revisionSuffix : null
      containers: [
        {
          name: 'frontend'
          image: frontendImage
          resources: {
            cpu: json('0.25') // Minimum CPU
            memory: '0.5Gi'   // Minimum memory
          }
          env: [
            {
              name: 'VITE_API_BASE'
              value: 'https://${backendFqdn}'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0 // Scale to 0 for all environments to minimize cost
        maxReplicas: 1 // Limit to 1 replica maximum
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

output frontendUrl string = 'https://${frontendFqdn}'
output backendUrl string = 'https://${backendFqdn}'
output cosmosAccountName string = cosmosAccount.name
output storageAccountName string = storageAccount.name
output resourceGroupName string = resourceGroupName
