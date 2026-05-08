@description('Environment name: dev, staging, prod')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'prod'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Workload short name — used in all resource names')
param workload string = 'standort'

@description('CosmosDB database name')
param cosmosDbName string = 'standort'

// ─── Derived names ───────────────────────────────────────────────────────────
var storageAccountName  = 'st${workload}${environment}'   // no hyphens allowed
var appServicePlanName  = 'asp-${workload}-${environment}'
var functionAppName     = 'func-${workload}-${environment}'
var cosmosAccountName   = 'cosmos-${workload}-${environment}'
var staticWebAppName    = 'stapp-${workload}-${environment}'
var appInsightsName     = 'appi-${workload}-${environment}'
var logAnalyticsName    = 'log-${workload}-${environment}'

// ─── Log Analytics Workspace (required by App Insights v2) ───────────────────
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ─── Application Insights ─────────────────────────────────────────────────────
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ─── Storage Account (required by Azure Functions runtime) ───────────────────
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

// ─── CosmosDB Account ─────────────────────────────────────────────────────────
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    enableFreeTier: true
    capabilities: [
      { name: 'EnableServerless' }  // serverless = pay-per-request, ideal for low traffic
    ]
  }
}

// ─── CosmosDB Database ────────────────────────────────────────────────────────
resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: cosmosDbName
  properties: {
    resource: {
      id: cosmosDbName
    }
  }
}

// ─── CosmosDB Containers ──────────────────────────────────────────────────────
resource cosmosGroupsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'groups'
  properties: {
    resource: {
      id: 'groups'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
  }
}

resource cosmosInviteCodesContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'inviteCodes'
  properties: {
    resource: {
      id: 'inviteCodes'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
  }
}

// ─── App Service Plan (Consumption = serverless, cheapest) ───────────────────
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'Y1'    // Consumption plan — pay per execution
    tier: 'Dynamic'
  }
  properties: {}
}

// ─── Azure Function App ───────────────────────────────────────────────────────
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v9.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'COSMOSDB_ACCOUNT_ENDPOINT'
          value: cosmosAccount.properties.documentEndpoint
        }
        {
          name: 'COSMOSDB_ACCOUNT_KEY'
          value: cosmosAccount.listKeys().primaryMasterKey
        }
        {
          name: 'CosmosDbConnectionString'
          value: 'AccountEndpoint=${cosmosAccount.properties.documentEndpoint};AccountKey=${cosmosAccount.listKeys().primaryMasterKey}'
        }
        {
          name: 'COSMOSDB_DATABASE_NAME'
          value: cosmosDbName
        }
        {
          name: 'COSMOSDB_GROUPS_CONTAINER'
          value: 'groups'
        }
        {
          name: 'COSMOSDB_INVITECODES_CONTAINER'
          value: 'inviteCodes'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
      cors: {
        // After deploying the Static Web App, replace * with its URL
        allowedOrigins: ['https://*.azurestaticapps.net']
        supportCredentials: false
      }
    }
    httpsOnly: true
  }
}

// ─── Azure Static Web App ─────────────────────────────────────────────────────
// NOTE: This provisions the resource. Deploy frontend code via:
//   npx @azure/static-web-apps-cli deploy ./dist --deployment-token <token>
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: 'eastus2'  // SWA only available in: westus2, centralus, eastus2, westeurope, eastasia
  sku: {
    name: 'Free'      // Free tier: 100 GB bandwidth, custom domains included
    tier: 'Free'
  }
  properties: {
    buildProperties: {
      appLocation: '/'
      outputLocation: 'dist'
    }
  }
}

// ─── Outputs ──────────────────────────────────────────────────────────────────
output functionAppUrl string             = 'https://${functionApp.properties.defaultHostName}'
output staticWebAppUrl string            = 'https://${staticWebApp.properties.defaultHostname}'
output cosmosEndpoint string             = cosmosAccount.properties.documentEndpoint
output staticWebAppDeployToken string    = staticWebApp.listSecrets().properties.apiKey
