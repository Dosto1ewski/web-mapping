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

## Recently implemented

### Marker feature (V1)

Members can create, read, and delete fixed markers (points of interest) within a group:

- **Create marker** `POST /api/groups/{groupId}/members/{memberId}/markers` — requires member token; creates a named location with optional color and notes
- **Fetch markers** `GET /api/groups/{groupId}/markers` — no auth required; returns all markers for a group
- **Delete marker** `DELETE /api/groups/{groupId}/members/{memberId}/markers/{markerId}` — requires member token; any member can delete any marker (no per-marker ownership in V1)

Markers are persisted in a separate Cosmos DB container (`markers`) with `markerId` as the unique identifier and `groupId` as the partition key. Each marker includes `createdByMemberId` and `createdAt` for audit purposes.

Future enhancements: per-marker ownership checks (delete only own marker), marker icons for differentiation.

---

### Eigener Standort als Live-State (Frontend)

Bisher wurde der eigene Standort zusammen mit allen anderen Gruppenmigliedern über den Server-Poll (`GET /locations`) auf der Karte angezeigt. Das bedeutete: nach dem Teilen des Standorts konnte es bis zu `locationFetchSec` Sekunden dauern, bis der eigene Marker auf der Karte aktualisiert wurde.

**Neue Logik:**

- Sobald der Nutzer seinen Standort teilt (GPS-Auto-Share, manueller Button, oder manuelles Setzen), wird die Position sofort in einem lokalen `ownLocation`-State in `App.tsx` gespeichert.
- Der eigene Member wird im Poll-Result (`usePollLocations`) auf der Karte **ignoriert** — `MapView` filtert ihn anhand der `session.memberId` heraus.
- Stattdessen wird ein eigener Live-Marker gerendert, der direkt aus `ownLocation` kommt — ohne Umweg über den Server-Poll.
- Der eigene Marker hat ein leicht anderes Icon (Glow-Ring), damit er visuell als "ich" erkennbar ist.

**Warum das wichtig ist für Navigation:** Für Routing/ETA muss das Frontend den eigenen Standort so aktuell wie möglich kennen. Mit diesem Design ist `ownLocation` immer exakt so frisch wie die letzte GPS-Messung — unabhängig vom Poll-Intervall.

**Betroffene Dateien:**
- `MemberControls.tsx` — neuer `onOwnLocation`-Callback, der in `sendLocation` sofort gefeuert wird
- `App.tsx` — `ownLocation`-State; wird bei Verlassen/Session-Ablauf zurückgesetzt
- `MapView.tsx` — eigener Member wird aus der Poll-Liste gefiltert; separater Live-Marker mit `createOwnIcon`

---

## Future tasks

## Bugs
- [X] **Sometimes the location of others users is not displayed**
### Security hardening

- [ ] **Read token for GET /locations** — require a group-level read token so that knowing `groupId` alone is not sufficient. Useful once groups are used in more public contexts.
- [ ] **Invite code revocation / rotation** — allow the group creator to invalidate the current invite code and issue a new one without dissolving the group.
- [ ] **Rate limiting** — per-member leaky bucket on `PUT /location` to prevent flood updates; can be implemented as ASP.NET Core middleware or an API Management policy.
- [ ] **Token expiry** — add `TokenExpiresAt` to `Member`; `401` on expired tokens to force re-join. Useful for session hygiene in long-running groups.
- [ ] **Session ID retry limit** — Rate-limit trying groupIDs, for joining a session.
- [ ] **Audit log** — append-only log of token rotations and joins, stored as separate Cosmos documents, for debugging "who took my session" issues.

### Features
- [X] **User Management** — Allow Update location only from the last browser section. "Logout" all other session, when they try to use this method. To assure only one 
  Active session per username.
- [X] **Jump to my current location Button** Wie der blaue Pfeil in Maps unten rechts
- [X] **Navigation / routing to Markers** — frontend shows route and ETA to markers. Voraussetzung (eigener Live-Standort) ist implementiert.
Using graphhopper:
POST: https://graphhopper.com/api/l/route
Example Body:
```
{
  "points": [
    [
      11.539421,
      48.118477
    ],
    [
      11.559023,
      48.12228
    ]
  ],
  "snap_preventions": [
    "motorway",
    "ferry",
    "tunnel"
  ],
  "details": [
    "road_class",
    "surface"
  ],
  "profile": "car",
  "locale": "en",
  "instructions": true,
  "calc_points": true,
  "points_encoded": false
}
```
- [ ] **Navigation / routing to other users** — frontend shows route and ETA to group members. Cool wäre dann eine "Gruppenmemberliste zu haben, in der man auf Navigate to Member klicken kann, damit man nicht auf der Karte nach User suchen muss.
  - [ ] **Group-Member-List** Which shows all Members of the group and when they last updatet their location.
- [ ] **Marker List** Shows a list of all Markers, so you don't have to search for them on the map
- [ ] **Invitelink statt URL + TOKEN** — 

### Design
- [ ] **Improve design, and UX** — Improve design, and UX
- [X] **Pop Up** — einklappbar
- [ ] **Icons for user Markers** — Allow users to select an icon to make differentiating between markers easier
- [x] **Show Nametags** — Have a checkbox in Settings, which makes it, that the User-Location-markers have their nametag hovering besides them. Default is: on
- [ ] **Show Nametag for Marker** Similar to user Tag
- [ ] **Improved History of user** Not last 5 pings, but rather last 10mins?
- [ ] **Icon for mode of travel**

Only in the Future:
### Real-time
- [ ] **SignalR push** — replace polling with Azure SignalR Service. The `PUT /location` write path already has a clear commit point; a SignalR hub notification can be emitted there. Polling endpoint stays as fallback.
- [ ] **Server-Sent Events (SSE)** — lighter-weight alternative to SignalR for read-only push. Function holds open an SSE stream and pushes on group version change.
