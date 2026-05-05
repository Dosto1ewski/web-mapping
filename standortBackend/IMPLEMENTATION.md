# Standort Backend — Implementation Overview

This document describes what has been built, how it works, and what comes next. It is the living reference for ongoing development.

---

## What is this?

A REST backend for a group-based location-sharing app. Members create or join a group, then periodically push their GPS position. Clients poll for updates. The backend is implemented as .NET 9 Azure Functions backed by Azure Cosmos DB.

---

## Solution structure

```
standortBackend/
  Standort.sln
  Directory.Build.props          global MSBuild properties
  .gitignore
  .editorconfig
  README.md                      emulator setup + curl examples
  IMPLEMENTATION.md              ← this file
  src/
    Standort.Domain/             entities, value objects, domain exceptions — no NuGet deps
    Standort.Application/        DTOs, interfaces, validators, services — pure logic
    Standort.Infrastructure/     Cosmos DB repositories, token hasher, clock, invite code gen
    Standort.Functions/          Azure Functions HTTP endpoints, DI wiring
  tests/
    Standort.UnitTests/          42 unit tests, no external dependencies
```

**Project dependency order:** `Domain ← Application ← Infrastructure ← Functions`  
Test project references `Application` and `Infrastructure` only.

---

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 9, Azure Functions Isolated Worker v4 |
| HTTP model | ASP.NET Core integration (`HttpRequest` / `IActionResult`) |
| Database | Azure Cosmos DB SDK 3.x, System.Text.Json camelCase serialization |
| Validation | FluentValidation v12 |
| Token security | SHA-256 hash, constant-time verify (`CryptographicOperations.FixedTimeEquals`) |
| Testing | xUnit + FluentAssertions + NSubstitute |

---

## Domain model

### Entities

**`Group`** — `sealed record` with `GroupId`, `Name`, `CreatedAt`, `Version` (monotone long).

**`Member`** — `sealed record` with `MemberId`, `GroupId`, `DisplayName`, `DisplayNameNormalized`, `TokenHash`, `TokenIssuedAt`, `CurrentLocation?`, `RecentHistory` (max 5), `LastUpdatedVersion`.

**`GeoCoordinate`** — `sealed record` value object: `Lat`, `Lng`, `AccuracyMeters`, `RecordedAt` (client timestamp), `ServerReceivedAt` (server clock).

### Domain exceptions

| Exception | When thrown |
|---|---|
| `GroupNotFoundException` | group doc not found in Cosmos |
| `MemberNotFoundException` | member doc not found |
| `InvalidTokenException` | token hash mismatch on location update |
| `InviteCodeNotFoundException` | invite code lookup returns null |
| `ConcurrencyException` | TransactionalBatch still failing after 3 retries |

---

## Data model (Cosmos DB)

### Container `groups` — partition key `/groupId`, `DefaultTimeToLive = -1`

Two document types within the same partition, discriminated by a `type` field.

**Group doc** (`id = "group"` — literal string, one per group):
```json
{ "id": "group", "type": "group", "groupId": "...", "name": "...", "createdAt": "...", "version": 0 }
```

**Member doc** (`id = "member:{memberId}"`):
```json
{
  "id": "member:ABCDEF1234567890",
  "type": "member",
  "groupId": "...",
  "memberId": "ABCDEF1234567890",
  "displayName": "Antonin",
  "displayNameNormalized": "antonin",
  "tokenHash": "<sha256-base64>",
  "tokenIssuedAt": "...",
  "currentLocation": { "lat": 49.0, "lng": 8.4, "accuracyMeters": 18, "recordedAt": "...", "serverReceivedAt": "..." },
  "recentHistory": [ /* up to 5, oldest first */ ],
  "lastUpdatedVersion": 5
}
```

### Container `inviteCodes` — partition key `/inviteCode`, `DefaultTimeToLive = -1`

```json
{ "id": "7H4K-P9QD", "inviteCode": "7H4K-P9QD", "groupId": "...", "createdAt": "..." }
```

Uniqueness is enforced implicitly by `id`; the generator retries on `409 Conflict`.

### Member ID strategy (race-free reclaim)

```
memberId = Crockford-Base32( SHA-256( groupId + "|" + normalizedDisplayName )[0..10 bytes] )
```

Produces a stable 16-character ID deterministic on (group, nickname). Two concurrent joins with the same nickname target the same Cosmos document key — ETag preconditions in the `TransactionalBatch` serialize them. No race window for duplicate member creation.

---

## Write path — atomic version increment

`PUT /location` goes through `CosmosGroupRepository.ApplyMemberWriteAsync`:

1. Read group doc → capture ETag.
2. Read member doc → capture ETag (may not exist for a new join).
3. Call the service's pure mutator `Func<Group, Member?, Member>`.
4. Set `LastUpdatedVersion = group.Version + 1`.
5. `TransactionalBatch` in the group's partition:
   - `PatchItem("group", Increment("/version", 1), IfMatchEtag)` — atomic increment.
   - `CreateItem(memberDoc)` if new, or `ReplaceItem(memberDocId, IfMatchEtag)` if existing.
6. On `412 PreconditionFailed` or `409 Conflict` → retry (up to 3 times). On persistent failure → `ConcurrencyException` → 409.

This guarantees `version` is monotone and each member write is stamped with the exact version it was committed at, with no gap between the two operations.

## Read path — 304 fast-path

`GET /locations?sinceVersion=N`:

1. Point-read group doc (~1 RU).
2. If `group.Version == sinceVersion` → return `null` → caller responds `304 Not Modified`.
3. Otherwise, single-partition query: `SELECT * FROM c WHERE c.type = 'member' AND c.lastUpdatedVersion > @v`.
4. Return `GroupLocationsResponse` with current `version` and member array.

Omitting `sinceVersion` returns a full snapshot (threshold = −1).

---

## Application services

### `GroupService`

- **`CreateGroupAsync`**: generates group GUID, member ID, plain token (32-byte CSPRNG, Base64Url), hashes token (SHA-256), generates invite code (Crockford-Base32 `XXXX-XXXX`), retries on invite code collision, writes group + member + invite code atomically.
- **`JoinGroupAsync`**: resolves invite code → groupId, derives member ID deterministically, calls `ApplyMemberWriteAsync` with a mutator that either creates a fresh member or reclaims an existing one (rotates token, preserves `CurrentLocation` + `RecentHistory`).

### `LocationService`

- **`UpdateLocationAsync`**: reads member for token verification, then writes via `ApplyMemberWriteAsync`. Mutator appends `CurrentLocation` to `RecentHistory` (`BuildShiftedHistory`) and sets the new point. History is capped at `RecentHistoryMax = 5` (oldest entry dropped).
- **`GetLocationsAsync`**: implements the 304 fast-path + member query described above.

---

## Security details

| Concern | Implementation |
|---|---|
| Member token | 32-byte `RandomNumberGenerator.GetBytes`, Base64Url-encoded; returned once in plaintext; stored as `SHA-256` hash (Base64) |
| Token verification | `CryptographicOperations.FixedTimeEquals` — constant-time, no timing oracle |
| Invite code | 8 Crockford-Base32 chars in `XXXX-XXXX` form; alphabet excludes I, L, O, U to avoid visual ambiguity; unique constraint via Cosmos `id` |
| Authorization model | Functions run at `AuthorizationLevel.Anonymous`; token auth is enforced in the Application layer (testable, no Azure-specific auth coupling) |
| GET endpoint | No token required in V1 — knowing the `groupId` is sufficient to read. Intentional simplification; see future tasks. |

---

## Validation (FluentValidation)

| Validator | Rules |
|---|---|
| `CreateGroupRequestValidator` | `Name` non-empty ≤ 100 chars; `DisplayName` 1–32 chars, letters/digits/spaces/`-_.` only |
| `JoinGroupRequestValidator` | Invite code must match `^[0-9A-HJ-NP-TV-Z]{4}-[0-9A-HJ-NP-TV-Z]{4}$`; same display name rules |
| `UpdateLocationRequestValidator` | `lat` ∈ [−90, 90]; `lng` ∈ [−180, 180]; `accuracyMeters` ≥ 0; `recordedAt` ≤ now + 60 s and ≥ now − 24 h |

Validators are injected into Functions and return `400 Bad Request` with a structured field-error list on failure.

---

## HTTP endpoints

| Method | Route | Auth | Response |
|---|---|---|---|
| `POST` | `/api/groups` | — | `201` `{ groupId, inviteCode, memberId, memberToken, displayName }` |
| `POST` | `/api/groups/join` | — | `200` `{ groupId, memberId, memberToken, displayName }` |
| `PUT` | `/api/groups/{groupId}/members/{memberId}/location` | Bearer token | `204` |
| `GET` | `/api/groups/{groupId}/locations?sinceVersion=N` | — | `200` / `304` |

Error shape: `{ "type": "...", "title": "...", "status": 4xx, "errors": [...] }`.

---

## Infrastructure wiring (DI)

All registrations in `src/Standort.Functions/Program.cs`:

- `CosmosClient` — singleton; key-based for emulator (when `COSMOSDB_ACCOUNT_KEY` is set), `DefaultAzureCredential` otherwise (production path).
- `CosmosBootstrapper` — runs once at startup via `EnsureCreatedAsync`; creates database + both containers if they don't exist. Makes `func start` a one-command local setup.
- Repositories, services, validators — all registered; validators as `IValidator<T>` singletons.

CORS origins (`http://localhost:5173`, `http://localhost:3000`) are configured in `local.settings.json`.

---

## Tests (42 unit tests)

No external dependencies — all Cosmos interaction is mocked via NSubstitute.

| File | What it covers |
|---|---|
| `Sha256TokenHasherTests` | hash roundtrip, mismatch, constant-time smoke, edge inputs |
| `CrockfordInviteCodeGeneratorTests` | format (`XXXX-XXXX`), alphabet, uniqueness over 50 samples |
| `MemberIdFactoryTests` | determinism, differs by group/name, output length + Crockford alphabet |
| `DisplayNameNormalizerTests` | trim + lowercase |
| `ValidatorTests` | all three validators — boundary values, invalid chars, time range |
| `GroupServiceTests` | create returns token/invite code, invite-code collision retry, join unknown code, new member, reclaim rotates token + preserves location |
| `LocationServiceTests` | bad token, missing member, history shift, history cap at 5, 304 fast-path, group missing, returns members |

Run with:
```
dotnet test
```

---

## Future tasks

### Cloud deployment (V2-infra)

- [ ] **Bicep templates** — Cosmos DB account (serverless SKU), Function App (Consumption plan), App Service Plan, Storage Account, Key Vault for secrets.
- [ ] **GitHub Actions pipeline** — build → test → `az deployment` on merge to `main`.
- [ ] **Managed Identity wiring** — remove `AccountKey` from production config; grant Function's identity `Cosmos DB Built-in Data Contributor` on the account.
- [ ] **Azure Static Web Apps integration** — configure SWA to proxy `/api/*` to the Function App, or migrate to SWA Managed Functions if moving to that model.

### Security hardening

- [ ] **Read token for GET /locations** — require a group-level read token so that knowing `groupId` alone is not sufficient. Useful once groups are used in more public contexts.
- [ ] **Invite code revocation / rotation** — allow the group creator to invalidate the current invite code and issue a new one without dissolving the group.
- [ ] **Rate limiting** — per-member leaky bucket on `PUT /location` to prevent flood updates; can be implemented as ASP.NET Core middleware or an API Management policy.
- [ ] **Token expiry** — add `TokenExpiresAt` to `Member`; `401` on expired tokens to force re-join. Useful for session hygiene in long-running groups.
- [ ] **Audit log** — append-only log of token rotations and joins, stored as separate Cosmos documents, for debugging "who took my session" issues.

### Features

- [ ] **Stop sharing** — member sets their own `CurrentLocation = null` and clears `RecentHistory`; they remain in the group but appear as "location not shared". Implement as `DELETE /api/groups/{groupId}/members/{memberId}/location`.
- [ ] **Group lifecycle / expiry** — optional TTL per group (e.g. 48 h after last activity). Requires a `LastActivityAt` on the group doc and a background Function that runs on a timer.
- [ ] **Leave group** — member explicitly removes themselves; group doc version increments so polling clients see the removal.
- [ ] **Shared markers / pins** — group members can drop named pins on the map (separate document type in the `groups` container). Out of scope for V1 by design.
- [ ] **Navigation / routing** — backend stores a shared destination; frontend shows route and ETA. V2+ feature.

### Real-time

- [ ] **SignalR push** — replace polling with Azure SignalR Service. The `PUT /location` write path already has a clear commit point; a SignalR hub notification can be emitted there. Polling endpoint stays as fallback.
- [ ] **Server-Sent Events (SSE)** — lighter-weight alternative to SignalR for read-only push. Function holds open an SSE stream and pushes on group version change.

### Developer experience

- [ ] **Integration test project** (`Standort.IntegrationTests`) — tests that run against the Cosmos DB Emulator in CI. Gate on emulator availability so they don't break the unit-test suite.
- [ ] **Docker Compose setup** — `docker-compose.yml` spinning up the Cosmos emulator + `func start` so new contributors can start with one command.
- [ ] **OpenAPI / Swagger** — add `Microsoft.Azure.Functions.Worker.Extensions.OpenApi` and expose a Swagger UI at `/api/swagger` for frontend developers.
- [ ] **Structured logging** — replace implicit `ILogger` usage with structured log properties (groupId, memberId, version) so Application Insights traces are queryable.

### Observability

- [ ] **Application Insights wiring** — connect the Functions telemetry sink; add custom metrics for location update rate and 304 hit ratio.
- [ ] **Health check endpoint** — `GET /api/health` that verifies Cosmos connectivity; used by load balancers and uptime monitors.
