# Standort Backend

Real-time location-sharing backend for a group-based webapp. V1 serves a polling-based API; V2 will add SignalR push.

## Architecture

| Layer | Project |
|---|---|
| Domain entities & exceptions | `Standort.Domain` |
| DTOs, interfaces, validators, services | `Standort.Application` |
| Cosmos DB repositories, security helpers | `Standort.Infrastructure` |
| Azure Functions HTTP endpoints | `Standort.Functions` |

**Tech stack:** .NET 9 · Azure Functions Isolated Worker v4 · Azure Cosmos DB SDK 3.x · FluentValidation · xUnit + FluentAssertions + NSubstitute

## Local development

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azure Cosmos DB Emulator](https://learn.microsoft.com/azure/cosmos-db/local-emulator) running on `https://localhost:8081`

#### Zum Beispiel via Docker Desktop
`docker run --name cosmos-emulator -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254 -p 10255:10255 -m 3g --cpus=2.0 mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator`
// Ports: 8081 ist der Haupt-Port für das SDK und den Explorer. Die restlichen Ports (10251-10255) werden für die interne Kommunikation der Partitionen benötigt.
// Ressourcen: Der Emulator ist hungrig. -m 3g (3 GB RAM) und --cpus=2.0 sind das Minimum für eine flüssige Performance.

https://localhost:8081/_explorer/index.html
Zertifikat als .der runterladen und mit rechtsklick Zertifikat installieren für `Lokaler Computer`
Alle Zertifikate in folgendem Speicher speichern, nicht Zertifikatsspeicher automatisch auswählen und dann dort Ordner "Vertrauenswürdige Stammzertifizierungsstellen" wählen.

### First-time setup

1. Start the Cosmos DB Emulator.
2. Copy `local.settings.json.example` to `local.settings.json` inside `src/Standort.Functions/` (or create it — see template below). The file is gitignored.
3. Run `func start` from `src/Standort.Functions/`. The app creates the database and containers on startup if they don't exist.

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

```
dotnet test
```

All 42 unit tests run without any external dependencies.

## API

### Talend API Tester quick flow

Use this order because later requests need values returned by earlier requests.

Base URL:
```text
http://localhost:7071/api
```

#### 1. Create group

Method: `POST`

URL:
```text
http://localhost:7071/api/groups
```

Headers:
```text
Content-Type: application/json
```

Body:
```json
{
  "name": "Testgruppe",
  "createdByDisplayName": "Antonin"
}
```

Copy `groupId`, `inviteCode`, `memberId`, and `memberToken` from the response.

Example Response:
```json
{
"groupId": "ad4541df-ea08-423a-b464-8567cbbde86e",
"inviteCode": "K792-CM78",
"memberId": "BQP8JT4W174YH13A",
"memberToken": "SUUWxlMD6MZR9NP1O1LTmT30th3Y8Y5EYkJh-D-V6Zg",
"displayName": "Antonin"
}
```

#### 2. Join group

Method: `POST`

URL:
```text
http://localhost:7071/api/groups/join
```

Headers:
```text
Content-Type: application/json
```

Body:
```json
{
  "inviteCode": "ABCD-1234",
  "displayName": "Max"
}
```

Replace `ABCD-1234` with the `inviteCode` from step 1. Copy the returned `memberId` and `memberToken` if you want to update this member's location.

Example Response:
```json
{
"groupId": "ad4541df-ea08-423a-b464-8567cbbde86e",
"memberId": "P3SWDAQ45B5EH6TR",
"memberToken": "v5V93FJYU7QGEwh5Lbi5qC3BzRIecuWZp5wZhSYvbuY",
"displayName": "Max"
}
```

#### 3. Update location

Method: `PUT`

URL:
```text
http://localhost:7071/api/groups/{groupId}/members/{memberId}/location
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

Expected response: `204 No Content`.

#### 4. Get group locations

Method: `GET`

URL:
```text
http://localhost:7071/api/groups/{groupId}/locations
```

No body is required.

Optional polling URL:
```text
http://localhost:7071/api/groups/{groupId}/locations?sinceVersion=1
```

If nothing changed since that version, the API returns `304 Not Modified`.

---

### `POST /api/groups` — Create group

```
curl -X POST http://localhost:7071/api/groups \
  -H "Content-Type: application/json" \
  -d '{"name":"Wandertour Samstag","createdByDisplayName":"Antonin"}'
```

Response `201 Created`:
```json
{
  "groupId": "...",
  "inviteCode": "7H4K-P9QD",
  "memberId": "...",
  "memberToken": "<save this — shown once>",
  "displayName": "Antonin"
}
```

---

### `POST /api/groups/join` — Join group

```
curl -X POST http://localhost:7071/api/groups/join \
  -H "Content-Type: application/json" \
  -d '{"inviteCode":"7H4K-P9QD","displayName":"Mira"}'
```

Response `200 OK`:
```json
{
  "groupId": "...",
  "memberId": "...",
  "memberToken": "<save this>",
  "displayName": "Mira"
}
```

Rejoining with an existing display name rotates the token ("newest session wins"). The old token returns `401` on subsequent location updates.

---

### `PUT /api/groups/{groupId}/members/{memberId}/location` — Update location

```
curl -X PUT http://localhost:7071/api/groups/{groupId}/members/{memberId}/location \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <memberToken>" \
  -d '{"lat":49.0128,"lng":8.4416,"accuracyMeters":18,"recordedAt":"2026-05-05T10:15:00Z"}'
```

Response `204 No Content`.

Validation rules:
- `lat` ∈ [-90, 90], `lng` ∈ [-180, 180]
- `accuracyMeters` ≥ 0
- `recordedAt` must be within the last 24 h and at most 60 s in the future

The server keeps the last 5 location pings per member as a trail (`recentHistory`).

---

### `GET /api/groups/{groupId}/locations?sinceVersion=N` — Poll locations

```
curl "http://localhost:7071/api/groups/{groupId}/locations"
```

Response `200 OK`:
```json
{
  "groupId": "...",
  "version": 5,
  "members": [
    {
      "memberId": "...",
      "displayName": "Thomas",
      "currentLocation": {
        "lat": 49.0128,
        "lng": 8.4416,
        "accuracyMeters": 18,
        "recordedAt": "2026-05-05T10:15:00Z"
      },
      "recentHistory": [
        { "lat": 49.0120, "lng": 8.4410, "accuracyMeters": 20, "recordedAt": "..." }
      ]
    }
  ]
}
```

Pass `?sinceVersion=5` to get `304 Not Modified` when nothing has changed — the client should store the last received `version` and use it on every subsequent poll.

---

## Notes

- No auth on `GET /locations` in V1 — anyone who knows the `groupId` can read. A read-token will be added in V2 if needed.
- `inviteCode` format: 8 Crockford-Base32 characters in `XXXX-XXXX` form (no ambiguous I/L/O/U).
- Production deployment (Bicep, pipelines, DefaultAzureCredential RBAC) is out of scope for V1.
