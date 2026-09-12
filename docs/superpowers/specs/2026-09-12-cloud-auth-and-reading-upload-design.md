# Design: Cloud Accounts, Auth, and Reading Upload (Cosmos DB)

Date: 2026-09-12
Status: Approved

## Goal

Add multi-user accounts with hashed-password authentication and upload of locally stored ESP readings to an already-provisioned Azure Cosmos DB (Free Tier) account. Authentication is verified by a small backend, not the client. Each account owns its data. The app remains local-first: readings are always stored in SQLite and are pushed to the cloud only while logged in.

## Background / Constraints

- App: MAUI .NET 10, `sqlite-net-pcl` 1.9.172, `CommunityToolkit.Mvvm` 8.4.2.
- Readings already persist locally via `EspReadingRepository`; a pending-upload queue exists and is unused: `IsUploaded`, `GetPendingUploadsAsync`, `MarkUploadedAsync`, `GetPendingUploadCountAsync` (`Services/EspReadingRepository.cs`).
- Cosmos DB (Free Tier) is deployed. Its connection string is a secret and must never ship in the app: the app authenticates only to the backend, which holds the Cosmos credentials.
- Backend is a new Azure Functions project (C# isolated worker) added to `MAUI&IOT.slnx`. HTTP-triggered functions only.
- Azure Functions consumes the Cosmos connection string from an app setting; locally from `local.settings.json` (gitignored).
- No automated test framework exists in the project. Verification is local API calls + on-device.
- Azure Functions Core Tools (`func`) is not currently installed; required to run locally and to publish.

## Architecture

### 1. Backend: `MauiIot.Api` (new Azure Functions project)

C# isolated worker, HTTP-triggered functions. Holds the Cosmos connection string and the JWT signing key in app settings. Base route is the Functions default `https://<app>.azurewebsites.net/api/...`.

Endpoints:

| Method | Route | Auth | Body | Success | Failure |
|---|---|---|---|---|---|
| POST | `/api/auth/register` | none | `{ username, password }` | 201 `{ token, expiresAt, username }` | 400 invalid, 409 username taken |
| POST | `/api/auth/login` | none | `{ username, password }` | 200 `{ token, expiresAt, username }` | 401 generic |
| POST | `/api/readings` | Bearer JWT | `{ readings: [ ... ] }` | 200 `{ accepted }` | 400 invalid, 401 unauthorized |

Rules and details:

- **Username normalization**: `username.Trim().ToLowerInvariant()` is the account `id`. Display username is stored separately.
- **Password hashing**: `Microsoft.AspNetCore.Identity.PasswordHasher<T>` (PBKDF2, `HMACSHA512`, 100k iterations, random per-password salt). No third-party crypto dependency. Never store or log the raw password.
- **JWT**: HS256, signing key from `JwtSigningKey` app setting. Claims: `sub` = account id (normalized username), `name` = display username. Expiry 7 days. No refresh token; an expired token returns 401 and the client returns to Login.
- **Register** is auto-login: it returns a token on success.
- **`POST /api/readings`** accepts a batch. For each item the server:
  1. Sets `userId` from the `sub` claim (client-supplied `userId` is ignored).
  2. Sets `receivedAtUtc` = now; leaves `recordedAtUtc` as supplied by the client.
  3. Upserts idempotently using deterministic `id = $"{userId}:{deviceId}:{bootSessionId}:{readingId}"`.
  4. Returns the count of items written.
- Re-uploading the same batch is a no-op (same ids upserted), so client retries are safe.
- Secrets live in app settings: `CosmosConnectionString`, `CosmosDatabaseName`, `JwtSigningKey`. `local.settings.json` is gitignored; Azure app settings are set at deploy time.

### 2. Cosmos DB containers

| Container | Partition key | Document id | Purpose |
|---|---|---|---|
| `users` | `/id` | `id` = normalized username | Account lookup by point read; holds `passwordHash`, display name, `createdAtUtc` |
| `readings` | `/userId` | `{userId}:{deviceId}:{bootSessionId}:{readingId}` | Reading documents, one per reading, owned by a user |

Reading document shape (JSON):

```json
{
  "id": "<userId>:<deviceId>:<bootSessionId>:<readingId>",
  "userId": "<normalized username>",
  "deviceId": "<esp device id>",
  "bootSessionId": 123,
  "readingId": 456,
  "elapsedSeconds": 789,
  "temperatureCelsius": 24.5,
  "humidityPercent": 45.0,
  "latitude": 44.8,
  "longitude": 20.4,
  "recordedAtUtc": "2026-09-12T10:00:00+00:00",
  "receivedAtUtc": "2026-09-12T10:00:05+00:00"
}
```

The partition key `/userId` matches every cloud read (per-user) and spreads writes across accounts.

### 3. MAUI app

New services (all registered in `MauiProgram.cs`):

- **`IAuthService` / `AuthService`** — `RegisterAsync`, `LoginAsync`, `LogoutAsync`, `CurrentUser`, `HasValidSession`. Persists `{ token, username, expiresAt }` in `SecureStorage`. Raises a change notification/event so the upload loop can start/stop.
- **`ApiClient`** — thin `HttpClient` wrapper. Base URL from config; attaches `Authorization: Bearer <token>` when present; serializes/deserializes with `System.Text.Json`; maps non-2xx responses to typed results. Owns the single `HttpClient`.
- **`ReadingUploadService`** — background loop (same shape as `EspAutoSyncService`): while `HasValidSession`, take `GetPendingUploadsAsync(200)`, map to the API payload, `POST /api/readings`, and on success `MarkUploadedAsync`. On failure it leaves readings pending and retries with backoff. 401 clears the session and stops until next login.

New UI:

- **`LoginPage` / `LoginPageModel`** — username, password, Login button, link to Register, error label.
- **`RegisterPage` / `RegisterPageModel`** — username, password, confirm password, Create button, link back to Login.
- **Startup gating**: on launch, if `HasValidSession` is false, navigate to `LoginPage` and prevent access to the main tabs; otherwise show the main tabs. Login/Register are Shell routes outside the tab content.
- **Account area on Dashboard**: shows the logged-in username and a Logout button.

### 4. Data flow

1. Launch → `AuthService` loads the session from `SecureStorage`. Valid → main tabs. Missing/expired → Login.
2. Register or Login → `ApiClient` → backend verifies → returns JWT → stored in `SecureStorage`.
3. Existing BLE auto-sync stores readings locally with `IsUploaded = false`.
4. `ReadingUploadService` drains pending readings while logged in → `POST /api/readings` with the bearer token → backend stamps `userId` → Cosmos upsert → app calls `MarkUploadedAsync`.
5. Logout → clear the session, stop the upload loop; unsent readings remain queued locally.

**Ownership decision (Option A): readings are tagged at upload time.** Readings collected while logged out are attributed to whichever account is logged in at upload. Acceptable for one-account-per-phone; noted as a known limitation for shared phones.

## Error Handling

- API returns `400` (validation), `401` (bad credentials / missing or expired token), `409` (username taken), `500` (unexpected). Responses never include stack traces, internal ids, or Cosmos details.
- Login and register failures show a generic message on the page; no enumeration of existing usernames beyond the 409 on register.
- App: network failure → keep readings pending, show a non-blocking status, retry with backoff. `401` → clear session, return to Login.
- Cosmos `429`/transient → the Functions SDK retry policy plus client retry covers it; the deterministic ids make retries safe.
- Passwords and tokens are never logged.

## Testing & Verification

- Build: `dotnet build "MAUI&IOT.slnx"` (0 errors) and `dotnet build` of `MauiIot.Api`.
- Backend against the real Cosmos via `func start`: exercise register / duplicate register / login / wrong password / authorized readings / unauthorized readings with a REST client; confirm documents in Cosmos Data Explorer and that a repeated batch does not duplicate.
- On device: register → session persists across relaunch → synced readings appear in Cosmos → logout pauses upload → re-login resumes and flushes the backlog.
- Optional follow-up: xUnit project covering password hashing and JWT validation.

## Out of Scope

- Email verification, password reset, refresh tokens, roles/permissions.
- The `IDeviceAuthorizationService` seam stays a stub (future: authorize ESP device IDs per account).
- Cloud → device commands, real-time push, multi-device account sharing.
- Managed identity for Cosmos (connection string in app settings is the chosen approach for this pass).

## Build / Deploy Notes

- Target framework for `MauiIot.Api`: `net10.0` (SDK 10.0.401 installed).
- Local run/publish requires Azure Functions Core Tools (`func`), not currently installed (`npm i -g azure-functions-core-tools@4` or the MSI).
- The mobile app's API base URL differs between emulator/device and local/production; it lives in one config constant.
