# Azure Functions + Cosmos DB Projekt-Template
Diese Vorlage beschreibt eine sinnvolle Projektstruktur fuer ein neues .NET Azure Functions Backend, das fachliche Daten in Azure Cosmos DB ablegt.

## Zielbild

Das Backend bleibt in klar getrennten Layern aufgebaut:

```text
<repo-root>/
  src/
    <Project>.Domain/
    <Project>.Application/
    <Project>.Infrastructure/
    <Project>.Functions/
  tests/
    UnitTests/
  infrastructure/
    infrastructure.bicep
    infrastructure.parameters.d.json
    bicepconfig.json
  pipelines/
    pipeline-build.yml
    pipeline-deploy.yml
    job_build_functionapp.yml
    job_build_infrastructure.yml
    job_deploy_functionapp.yml
    job_deploy_infrastructure.yml
  docs/
  README.md
  <Project>.sln
```

Wichtig: Cosmos DB ersetzt den fachlichen Storage fuer die Anwendung. Eine Azure Function App braucht aber weiterhin ein Storage Account fuer den Functions-Host, zum Beispiel fuer `AzureWebJobsStorage`, Function Keys, Trigger-Zustand und je nach Hosting-Plan Azure Files. Dieses Storage Account sollte im neuen Projekt nur Runtime-Infrastruktur sein, nicht der Ort fuer fachliche Daten.

## Projektstruktur

### `src/<Project>.Domain`

Enthaelt die fachlichen Kernobjekte ohne Azure-, HTTP- oder Datenbank-Abhaengigkeiten.

Typische Inhalte:

```text
Entities/
ValueObjects/
Enums/
DomainExceptions/
```

Regel: Domain kennt weder Cosmos DB noch Azure Functions.

### `src/<Project>.Application`

Enthaelt die Use Cases, DTOs, Validatoren und Interfaces, die von Infrastructure implementiert werden.

Typische Inhalte:

```text
DTOs/
Interfaces/
Models/
Services/
Validators/
Exceptions/
```

Beispiel fuer Cosmos DB:

```csharp
public interface IProjectRepository
{
    Task<ProjectItem?> GetAsync(string id, string partitionKey, CancellationToken cancellationToken);
    Task UpsertAsync(ProjectItem item, CancellationToken cancellationToken);
}
```

Regel: Application definiert, was benoetigt wird. Infrastructure entscheidet, wie Cosmos DB angesprochen wird.

### `src/<Project>.Infrastructure`

Enthaelt Implementierungen fuer externe Systeme: Cosmos DB, Azure OpenAI, Azure DevOps, Mail, HTTP-Clients oder andere Integrationen.

Typische Inhalte:

```text
Configuration/
CosmosDb/
ExternalServices/
```

Empfohlene Cosmos DB Struktur:

```text
Infrastructure/
  Configuration/
    CosmosDbOptions.cs
  CosmosDb/
    CosmosDbClientFactory.cs
    CosmosProjectRepository.cs
```

Empfohlene Settings:

```csharp
public sealed class CosmosDbOptions
{
    public string AccountEndpoint { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
}
```

Wenn moeglich, Managed Identity statt Connection String verwenden:

```csharp
var client = new CosmosClient(
    options.AccountEndpoint,
    new DefaultAzureCredential());
```

### `src/<Project>.Functions`

Enthaelt die Azure Functions, `Program.cs`, `host.json` und die DI-Registrierung.

Typische Inhalte:

```text
Program.cs
host.json
CreateItemFunction.cs
GetItemFunction.cs
UpdateItemFunction.cs
```

Function-Klassen sollten duenn bleiben:

1. Request lesen und deserialisieren.
2. Request validieren.
3. Application-Service aufrufen.
4. HTTP Response erzeugen.
5. Fehler in konsistente Error Responses uebersetzen.

`Program.cs` ist der zentrale Ort fuer:

```text
Options aus App Settings
HTTP Clients
CosmosClient oder Cosmos-Repositories
Application Services
Validatoren
Logging
```

Beispiel fuer Cosmos DB DI:

```csharp
services.Configure<CosmosDbOptions>(options =>
{
    options.AccountEndpoint = configuration["COSMOSDB_ACCOUNT_ENDPOINT"] ?? string.Empty;
    options.DatabaseName = configuration["COSMOSDB_DATABASE_NAME"] ?? string.Empty;
    options.ContainerName = configuration["COSMOSDB_CONTAINER_NAME"] ?? string.Empty;
});

services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
    return new CosmosClient(options.AccountEndpoint, new DefaultAzureCredential());
});

services.AddSingleton<IProjectRepository, CosmosProjectRepository>();
```

### `tests/UnitTests`

Tests folgen den Layern aus `src`.

Typische Struktur:

```text
UnitTests/
  Application/
  Infrastructure/
  Functions/
  TestHelpers/
```

Fokus:

```text
Validatoren
Application Services
Cosmos Repository Mapping und Fehlerfaelle
Function Request/Response Verhalten
```

Cosmos DB sollte in Unit Tests nicht direkt gegen Azure laufen. Fuer echte Datenbanktests entweder Emulator/Testcontainer separat nutzen oder Integrationstests klar von Unit Tests trennen.

## Bicep-Struktur

### `infrastructure/infrastructure.bicep`

Zentrales Resource-Group-Deployment. Fuer ein Azure Functions + Cosmos DB Backend sollte diese Datei mindestens enthalten:

```text
Log Analytics Workspace
Application Insights
Storage Account fuer Azure Functions Runtime
App Service Plan oder Flex Consumption Plan
Function App
Managed Identity
Key Vault
Cosmos DB Account
Cosmos DB SQL Database
Cosmos DB SQL Container
Cosmos DB Data-Plane RBAC Role Assignment
Outputs fuer Pipeline und App-Konfiguration
```

Empfohlene Namensvariablen:

```bicep
param env string
param locationName string
@minLength(2)
@maxLength(15)
param nameSuffix string

var appShortName = 'myapp'
var logWorkspaceName = 'log-${appShortName}-${env}'
var appInsightsName = 'appi-${appShortName}-${env}'
var runtimeStorageAccountName = 'st${appShortName}${env}${nameSuffix}'
var appServicePlanName = 'asp-${appShortName}-${env}'
var functionAppName = 'func-${appShortName}-${env}-${nameSuffix}'
var managedIdentityName = 'id-${appShortName}-${env}-${nameSuffix}'
var keyVaultName = 'kv-${appShortName}-${env}-${nameSuffix}'
var cosmosAccountName = 'cosmos-${appShortName}-${env}-${nameSuffix}'
var cosmosDatabaseName = '${appShortName}-${env}'
var cosmosContainerName = 'items'
```

### Cosmos DB Bicep-Grundlage

Fuer Azure Cosmos DB for NoSQL:

```bicep
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2025-10-15' = {
  name: cosmosAccountName
  location: locationName
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: locationName
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    publicNetworkAccess: 'Enabled'
    enableFreeTier: env == 'd'
    disableLocalAuth: true
    minimalTlsVersion: 'Tls12'
  }
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2025-10-15' = {
  parent: cosmosAccount
  name: cosmosDatabaseName
  properties: {
    resource: {
      id: cosmosDatabaseName
    }
  }
}

resource cosmosContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2025-10-15' = {
  parent: cosmosDatabase
  name: cosmosContainerName
  properties: {
    resource: {
      id: cosmosContainerName
      partitionKey: {
        paths: [
          '/tenantId'
        ]
        kind: 'Hash'
      }
    }
    options: {
      autoscaleSettings: {
        maxThroughput: 1000
      }
    }
  }
}
```

Die Partition Key Strategie muss vor dem ersten echten Einsatz feststehen. Fuer viele Business-Apps ist `/tenantId`, `/customerId`, `/organizationId` oder eine fachliche Gruppierung besser als `/id`, weil sie Last und Abfragen kontrollierbarer verteilt.

### Cosmos DB RBAC

Wenn die Function App per Managed Identity auf Cosmos DB zugreift, braucht sie eine Cosmos DB SQL Role Assignment. Fuer einfache CRUD-Backends kann die eingebaute Rolle `Cosmos DB Built-in Data Contributor` verwendet werden.

```bicep
resource cosmosDataContributorRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions@2025-10-15' existing = {
  parent: cosmosAccount
  name: '00000000-0000-0000-0000-000000000002'
}

resource functionAppCosmosDataContributor 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2025-10-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, functionApp.id, 'cosmos-data-contributor')
  properties: {
    principalId: functionApp.identity.principalId
    roleDefinitionId: cosmosDataContributorRole.id
    scope: cosmosAccount.id
  }
}
```

Fuer produktive Systeme ist eine Custom Role oft besser, wenn die Function App nicht alle Datenaktionen benoetigt.

### Function App Settings

Die Function App braucht weiterhin Host-Storage-Settings:

```bicep
{ name: 'AzureWebJobsStorage', value: runtimeStorageConnectionString }
{ name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING', value: runtimeStorageConnectionString }
{ name: 'WEBSITE_CONTENTSHARE', value: functionAppName }
{ name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
{ name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
{ name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
```

Fachliche Cosmos DB Settings:

```bicep
{ name: 'COSMOSDB_ACCOUNT_ENDPOINT', value: cosmosAccount.properties.documentEndpoint }
{ name: 'COSMOSDB_DATABASE_NAME', value: cosmosDatabaseName }
{ name: 'COSMOSDB_CONTAINER_NAME', value: cosmosContainerName }
```

Wenn `disableLocalAuth: true` gesetzt ist, keine Cosmos DB Keys oder Connection Strings in App Settings speichern. Zugriff laeuft dann ueber Managed Identity und RBAC.

### Outputs

Nuetzliche Outputs fuer Pipelines und manuelle Kontrolle:

```bicep
output functionAppName string = functionApp.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output managedIdentityPrincipalId string = functionApp.identity.principalId
output cosmosAccountName string = cosmosAccount.name
output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint
output cosmosDatabaseName string = cosmosDatabaseName
output cosmosContainerName string = cosmosContainerName
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
```

### `infrastructure/infrastructure.parameters.d.json`

Parameterdatei fuer die Dev-Umgebung. Stage und Prod sollten eigene Dateien bekommen:

```text
infrastructure.parameters.d.json
infrastructure.parameters.s.json
infrastructure.parameters.p.json
```

Beispiel:

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "env": { "value": "d" },
    "locationName": { "value": "germanywestcentral" },
    "nameSuffix": { "value": "afh01" }
  }
}
```

Keine Secrets in Parameterdateien ablegen. Secrets gehoeren in Key Vault oder in sichere Pipeline-Variablen.

### `infrastructure/bicepconfig.json`

Aktiviert Bicep Analyzer-Regeln. Empfehlung:

```json
{
  "analyzers": {
    "core": {
      "enabled": true,
      "rules": {
        "no-unused-params": {
          "level": "warning"
        },
        "use-recent-api-versions": {
          "level": "warning"
        },
        "use-recent-module-versions": {
          "level": "warning"
        }
      }
    }
  }
}
```

## Pipeline-Struktur

### Build Pipeline

`pipeline-build.yml` sollte zwei Jobs ausfuehren:

```text
job_build_infrastructure.yml
job_build_functionapp.yml
```

Infrastruktur-Build:

```text
az bicep build --file infrastructure/infrastructure.bicep
PublishBuildArtifacts: infrastructureDrop
```

Function-App-Build:

```text
dotnet restore
dotnet build
dotnet test
dotnet publish
PublishBuildArtifacts: functionAppDrop
```

### Deploy Pipeline

`pipeline-deploy.yml` sollte erst Infrastruktur deployen und danach die Function App:

```text
stage_infra
stage_functionapp
```

Deployment-Reihenfolge:

1. Bicep deployt Ressourcen, App Settings, RBAC und Outputs.
2. Function App Artifact wird auf die erzeugte Function App deployed.
3. Smoke Test oder Health Check gegen einen einfachen Endpoint.

## Lokale Entwicklung

Empfohlene lokale Settings in `local.settings.json`, nicht committen:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "COSMOSDB_ACCOUNT_ENDPOINT": "https://localhost:8081",
    "COSMOSDB_DATABASE_NAME": "myapp-local",
    "COSMOSDB_CONTAINER_NAME": "items"
  }
}
```

Optionen fuer lokale Cosmos DB Entwicklung:

```text
Azure Cosmos DB Emulator
echte Dev-Cosmos-DB in Azure
Repository-Mocks fuer Unit Tests
```

## Checkliste fuer ein neues Projekt

1. Solution und vier Projekte anlegen: Domain, Application, Infrastructure, Functions.
2. Projekt-Referenzen setzen: Functions -> Application + Infrastructure, Infrastructure -> Application, Application -> Domain.
3. DTOs, Validatoren und Interfaces in Application definieren.
4. CosmosDbOptions, CosmosClient und Repository-Implementierungen in Infrastructure bauen.
5. Functions als duenne HTTP- oder Trigger-Endpunkte schreiben.
6. `infrastructure.bicep` mit Host-Storage, Function App, Identity, Key Vault, Cosmos DB und RBAC aufsetzen.
7. Parameterdateien fuer `d`, `s`, `p` anlegen.
8. Build- und Deploy-Pipelines auf Bicep-Artefakt und Function-App-Artefakt ausrichten.
9. Unit Tests fuer Validatoren, Services und Repository-Mapping schreiben.
10. Vor dem ersten Produktivlauf Partition Keys, RU-Modell, Backup, Netzwerkzugriff und RBAC pruefen.

## Quellen fuer aktuelle Azure-Annahmen

- Azure Functions benoetigt ein Storage Account fuer den Runtime-Betrieb: https://learn.microsoft.com/en-us/azure/azure-functions/storage-considerations
- Azure Functions Cosmos DB Trigger und Bindings: https://learn.microsoft.com/azure/azure-functions/functions-bindings-cosmosdb-v2
- Bicep Resource Reference fuer Cosmos DB Accounts: https://learn.microsoft.com/en-us/azure/templates/microsoft.documentdb/databaseaccounts
- Bicep Resource Reference fuer Cosmos DB SQL Databases: https://learn.microsoft.com/en-us/azure/templates/microsoft.documentdb/databaseaccounts/sqldatabases
- Bicep Resource Reference fuer Cosmos DB SQL Containers: https://learn.microsoft.com/en-us/azure/templates/microsoft.documentdb/2025-10-15/databaseaccounts/sqldatabases/containers
- Bicep Resource Reference fuer Cosmos DB SQL RBAC: https://learn.microsoft.com/en-us/azure/templates/microsoft.documentdb/databaseaccounts/sqlroleassignments
