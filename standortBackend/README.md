# Standort Backend

Real-time location-sharing backend for a group-based webapp. V1 serves a polling-based API; V2 will add SignalR push.

## Architecture

| Layer | Project |
|---|---|
| Domain entities and exceptions | `Standort.Domain` |
| DTOs, interfaces, validators, services | `Standort.Application` |
| Cosmos DB repositories, security helpers | `Standort.Infrastructure` |
| Azure Functions HTTP endpoints | `Standort.Functions` |

**Tech stack:** .NET 9, Azure Functions Isolated Worker v4, Azure Cosmos DB SDK 3.x, FluentValidation, xUnit + FluentAssertions + NSubstitute

## Local development

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azure Cosmos DB Emulator](https://learn.microsoft.com/azure/cosmos-db/local-emulator) running on `https://localhost:8081`

#### Example with Docker Desktop

```powershell
docker run --name cosmos-emulator -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254 -p 10255:10255 -m 3g --cpus=2.0 mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```

Port `8081` is the main SDK and Explorer port. Ports `10251-10255` are used for internal partition communication. The emulator is resource hungry; `-m 3g` and `--cpus=2.0` are a practical minimum.

Open the Emulator Explorer:

```text
https://localhost:8081/_explorer/index.html
```

Download the `.der` certificate and install it for `Local Computer`. Choose the certificate store manually and select `Trusted Root Certification Authorities`.

### First-time setup

1. Start the Cosmos DB Emulator.
2. Copy `local.settings.json.example` to `local.settings.json` inside `src/Standort.Functions/`, or create it from the template below. The file is gitignored.
3. Run `func start` from `src/Standort.Functions/`. The app creates the database and containers on startup if they do not exist.

**`local.settings.json` template:**

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "COSMOSDB_ACCOUNT_ENDPOINT": "https://localhost:8081",
    "COSMOSDB_ACCOUNT_KEY": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "CosmosDbConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "COSMOSDB_DATABASE_NAME": "standort-local",
    "COSMOSDB_GROUPS_CONTAINER": "groups",
    "COSMOSDB_INVITECODES_CONTAINER": "inviteCodes"
  },
  "Host": {
    "CORS": "http://localhost:5173,http://localhost:3000",
    "CORSCredentials": false
  }
}
```

### Run tests

```powershell
dotnet test
```

All 48 unit tests run without any external dependencies.

## API

Use this order because later requests need values returned by earlier requests. The examples are written for Talend API Tester, but the same URLs and JSON bodies work with curl or Postman.

Base URL when running locally:

```text
http://localhost:7071/api
```

### 1. Create group

Method and URL:

```text
POST http://localhost:7071/api/groups
```

Headers:

```text
Content-Type: application/json
```

Body:

```json
{
  "name": "Wandertour Samstag",
  "createdByDisplayName": "Antonin"
}
```

Response `201 Created`:

```json
{
  "groupId": "ad4541df-ea08-423a-b464-8567cbbde86e",
  "inviteCode": "K792-CM78",
  "memberId": "BQP8JT4W174YH13A",
  "memberToken": "SUUWxlMD6MZR9NP1O1LTmT30th3Y8Y5EYkJh-D-V6Zg",
  "displayName": "Antonin",
  "historyDurationMinutes": 15
}
```

Copy `groupId`, `inviteCode`, `memberId`, and `memberToken` from the response. The `memberToken` is shown once and is required for location updates.

### 2. Join group

Method and URL:

```text
POST http://localhost:7071/api/groups/join
```

Headers:

```text
Content-Type: application/json
```

Body:

```json
{
  "inviteCode": "K792-CM78",
  "displayName": "Max"
}
```

Replace `K792-CM78` with the `inviteCode` returned by step 1.

Response `200 OK`:

```json
{
  "groupId": "ad4541df-ea08-423a-b464-8567cbbde86e",
  "memberId": "P3SWDAQ45B5EH6TR",
  "memberToken": "v5V93FJYU7QGEwh5Lbi5qC3BzRIecuWZp5wZhSYvbuY",
  "displayName": "Max",
  "historyDurationMinutes": 15
}
```

`historyDurationMinutes` reflects the member's stored history-window setting; for an existing
display name being reclaimed, the previously stored value is returned.

Copy this member's `memberId` and `memberToken` if you want to update this member's location. Rejoining with an existing display name rotates the token ("newest session wins"). The old token returns `401` on subsequent location updates.

### 3. Update location

Method and URL:

```text
PUT http://localhost:7071/api/groups/{groupId}/members/{memberId}/location
```

Headers:

```text
Content-Type: application/json
Authorization: Bearer {memberToken}
```

Body:

```json
{
  "lat": 52.520008,
  "lng": 13.404954,
  "accuracyMeters": 12.5,
  "recordedAt": "2026-05-06T10:00:00Z"
}
```

Replace `{groupId}`, `{memberId}`, and `{memberToken}` with values from a create/join response. `recordedAt` must be within the last 24 hours and at most 60 seconds in the future, so use the current UTC time when testing.

Response `204 No Content`.

Validation rules:

- `lat` in `[-90, 90]`, `lng` in `[-180, 180]`
- `accuracyMeters` must be `>= 0`
- `recordedAt` must be within the last 24 h and at most 60 s in the future

The server keeps a trail of past location pings per member (`recentHistory`). On every location
update the trail is pruned to the member's `historyDurationMinutes` window (see endpoint 8): any
ping whose `recordedAt` is older than `now - historyDurationMinutes` is dropped. Pruning happens
**only on a location update from that member** — a member who stops updating keeps their last
trail until they update again. A hard cap of 500 entries always applies as a safety net,
regardless of the chosen duration. The current location is always kept.

### 4. Get group locations

Method and URL:

```text
GET http://localhost:7071/api/groups/{groupId}/locations
```

No body is required.

Response `200 OK`:

```json
{
  "groupId": "ad4541df-ea08-423a-b464-8567cbbde86e",
  "version": 5,
  "members": [
    {
      "memberId": "P3SWDAQ45B5EH6TR",
      "displayName": "Max",
      "currentLocation": {
        "lat": 52.520008,
        "lng": 13.404954,
        "accuracyMeters": 12.5,
        "recordedAt": "2026-05-06T10:00:00Z"
      },
      "recentHistory": [
        {
            "lat": 49.0069,
            "lng": 8.404,
            "accuracyMeters": 25,
            "recordedAt": "2026-05-12T23:41:09.966+00:00",
            "serverReceivedAt": "2026-05-12T23:41:11.7797617+00:00"
        },
        {
            "lat": 49.0069,
            "lng": 8.409,
            "accuracyMeters": 25,
            "recordedAt": "2026-05-12T23:41:38.213+00:00",
            "serverReceivedAt": "2026-05-12T23:41:40.0210751+00:00"
        },
        {
            "lat": 49.0069,
            "lng": 8.409,
            "accuracyMeters": 25,
            "recordedAt": "2026-05-12T23:41:41.684+00:00",
            "serverReceivedAt": "2026-05-12T23:41:43.5025386+00:00"
        },
        {
            "lat": 49.0095293550832,
            "lng": 8.411664962768556,
            "accuracyMeters": 0,
            "recordedAt": "2026-05-12T23:53:05.483+00:00",
            "serverReceivedAt": "2026-05-12T23:53:07.369361+00:00"
        },
        {
            "lat": 49.009276007932016,
            "lng": 8.406858444213869,
            "accuracyMeters": 0,
            "recordedAt": "2026-05-12T23:53:21.653+00:00",
            "serverReceivedAt": "2026-05-12T23:53:23.4867437+00:00"
        }
      ],
    }
  ]
}
```

Optional polling URL:

```text
GET http://localhost:7071/api/groups/{groupId}/locations?sinceVersion=5
```

Pass the last received `version` as `sinceVersion`. If nothing changed since that version, the API returns `304 Not Modified`.

### 5. Create marker

Persists a fixed marker (point of interest) for the whole group. Every member of the group can place markers; deletion is also open to every member.

Method and URL:

```text
POST http://localhost:7071/api/groups/{groupId}/members/{memberId}/markers
```

Headers:

```text
Content-Type: application/json
Authorization: Bearer {memberToken}
```

Body:

```json
{
  "name": "Treffpunkt Parkplatz",
  "lat": 52.520008,
  "lng": 13.404954,
  "color": "#FF8800",
  "notes": "Großer Parkplatz hinter der Brücke"
}
```

`color` and `notes` are optional and may be omitted or set to `null`.

Response `201 Created`:

```json
{
  "markerId": "9f3c0a1c8b9d4f7a8e2d1f0a3b4c5d6e",
  "name": "Treffpunkt Parkplatz",
  "lat": 52.520008,
  "lng": 13.404954,
  "color": "#FF8800",
  "notes": "Großer Parkplatz hinter der Brücke",
  "createdByMemberId": "BQP8JT4W174YH13A",
  "createdAt": "2026-05-12T10:00:00Z"
}
```

Validation rules:

- `name` is required, max. 100 characters
- `lat` in `[-90, 90]`, `lng` in `[-180, 180]`
- `color` max. 50 characters (optional)
- `notes` max. 500 characters (optional)

Possible error responses:

- `400 Bad Request` — validation failed or malformed JSON
- `401 Unauthorized` — missing/invalid bearer token
- `404 Not Found` — group or member does not exist

### 6. Fetch all markers

Returns all markers belonging to a group. No authentication required, matching the read model of `GET /locations`.

Method and URL:

```text
GET http://localhost:7071/api/groups/{groupId}/markers
```

No body is required.

Response `200 OK`:

```json
[
  {
    "markerId": "9f3c0a1c8b9d4f7a8e2d1f0a3b4c5d6e",
    "name": "Treffpunkt Parkplatz",
    "lat": 52.520008,
    "lng": 13.404954,
    "color": "#FF8800",
    "notes": "Großer Parkplatz hinter der Brücke",
    "createdByMemberId": "BQP8JT4W174YH13A",
    "createdAt": "2026-05-12T10:00:00Z"
  },
  {
    "markerId": "5a1d8e4b7c3a2f9e0d8c1b2a3d4e5f60",
    "name": "Gipfel",
    "lat": 52.521234,
    "lng": 13.408765,
    "color": null,
    "notes": null,
    "createdByMemberId": "P3SWDAQ45B5EH6TR",
    "createdAt": "2026-05-12T10:05:23Z"
  }
]
```

If the group has no markers, the response is an empty array `[]`.

Possible error response:

- `404 Not Found` — group does not exist

### 7. Delete marker

Deletes a marker by its `markerId`. Any group member can delete any marker (no per-marker ownership check in V1).

Method and URL:

```text
DELETE http://localhost:7071/api/groups/{groupId}/members/{memberId}/markers/{markerId}
```

Headers:

```text
Authorization: Bearer {memberToken}
```

No body is required.

Response `204 No Content`.

Possible error responses:

- `401 Unauthorized` — missing/invalid bearer token
- `404 Not Found` — group, member, or marker does not exist

### 8. Update member settings

Sets the member's history-window preference (`historyDurationMinutes`). The value is stored but
existing history is **not** pruned immediately — pruning is applied on the member's next location
update (endpoint 3).

Method and URL:

```text
PUT http://localhost:7071/api/groups/{groupId}/members/{memberId}/settings
```

Headers:

```text
Content-Type: application/json
Authorization: Bearer {memberToken}
```

Body:

```json
{
  "historyDurationMinutes": 30
}
```

Response `204 No Content`.

Validation rules:

- `historyDurationMinutes` in `[0, 2880]` (0 = no history kept; 2880 = 48 h). Default is `15`.

Possible error responses:

- `400 Bad Request` — validation failed or malformed JSON
- `401 Unauthorized` — missing/invalid bearer token
- `404 Not Found` — group or member does not exist

### 9. Delete member history

Clears the member's stored trail (`recentHistory`) **and** their `currentLocation`. Use this to
fully stop sharing: the member disappears from the map until they send a new location update.
Auto-share is a client-side concept — the frontend additionally switches its auto-share toggle
off when this button is used.

Method and URL:

```text
DELETE http://localhost:7071/api/groups/{groupId}/members/{memberId}/history
```

Headers:

```text
Authorization: Bearer {memberToken}
```

No body is required.

Response `204 No Content`.

Possible error responses:

- `401 Unauthorized` — missing/invalid bearer token
- `404 Not Found` — group or member does not exist

## Deployment

### Prerequisites for Azure deployment

- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)

### Deploy infrastructure with Bicep

Bicep lives at `infra/main.bicep` in the repository root. Run all `az` commands from there.

```powershell
az deployment group create `
  --resource-group rg-standort-prod `
  --template-file infra/main.bicep
```

### Publish function code to Azure

After Bicep deployment succeeds, deploy your compiled function code:

```powershell
cd src/Standort.Functions
func azure functionapp publish func-standort-prod
```

Bicep only provisions infrastructure; this step deploys the actual function code (endpoints, handlers, etc.). Run this after:
- Adding or modifying HTTP endpoints (like marker endpoints)
- Updating business logic or validators
- Changing dependencies or configurations

### Full deployment workflow

1. Make code changes
2. Test locally with `func start` (after `dotnet build`)
3. Run Bicep to ensure infrastructure is up-to-date
4. Run `func azure functionapp publish func-standort-prod` to deploy code changes

The function app must exist in Azure before publishing; if it doesn't, Bicep will create it.

### Fixing the Cosmos DB partition key (one-time)

The `groups` container was initially created with partition key `/id` but the code expects `/groupId`. Cosmos DB does not allow changing the partition key on an existing container, so you must delete and recreate it:

1. Azure Portal → your Cosmos account → `standort` database → delete the `groups` container
2. Run the redeploy command above — it recreates the container with the correct `/groupId` partition key

## Notes

- No auth on `GET /locations` and `GET /markers` in V1: anyone who knows the `groupId` can read. A read-token will be added in V2 if needed.
- Marker deletion in V1 has no per-marker ownership: every group member can delete every marker. Per-creator authorization can be added in V2 if needed.
- `inviteCode` format: 8 Crockford-Base32 characters in `XXXX-XXXX` form, without ambiguous I/L/O/U characters.
- Production deployment with Bicep, pipelines, and DefaultAzureCredential RBAC is out of scope for V1.
