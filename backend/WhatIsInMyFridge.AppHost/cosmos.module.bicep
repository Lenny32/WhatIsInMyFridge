@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-08-15' = {
  name: take('cosmos-${uniqueString(resourceGroup().id)}', 44)
  location: location
  properties: {
    locations: [
      {
        locationName: location
        failoverPriority: 0
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    databaseAccountOfferType: 'Standard'
    disableLocalAuth: false
  }
  kind: 'GlobalDocumentDB'
  tags: {
    'aspire-resource-name': 'cosmos'
  }
}

resource WhatIsInMyFridge 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-08-15' = {
  name: 'WhatIsInMyFridge'
  location: location
  properties: {
    resource: {
      id: 'WhatIsInMyFridge'
    }
  }
  parent: cosmos
}

var accountKey = listKeys(cosmos.id, cosmos.apiVersion).primaryMasterKey
var endpoint = cosmos.properties.documentEndpoint

output connectionString string = 'AccountEndpoint=${endpoint};AccountKey=${accountKey}'

output name string = cosmos.name
