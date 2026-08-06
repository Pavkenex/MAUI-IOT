# Design: Auto-Discovery & Sync with Device Authorization

Date: 2026-08-07
Status: Approved

## Goal

Replace the manual BLE flow (Scan tab with Start Scan button, Sync tab with Connect/Sync buttons) with automatic discovery: while the app is open, nearby ESP32 DHT22 loggers are scanned continuously; any device whose ID is authorized (currently: all devices, via a stub) is connected to and its readings are downloaded automatically. The Sensors tab must show real readings from SQLite instead of mock data.

## Background / Constraints

- App: MAUI .NET 10, `Plugin.BLE` 3.2.1, `sqlite-net-pcl` 1.9.172.
- Real readings already land in SQLite via `EspReadingRepository`; `EspBluetoothService` already implements connect/sync with cursor-based delta sync (`REQUEST_AFTER`), idempotent inserts, boot-session detection, and a 120 s synchronization timeout.
- Working tree contains uncommitted fixes (advertisement-record detection in `IsEspDevice`, idempotent insert in `EspReadingRepository`, encoder cast, `ScanPageModel` navigation route). These are the baseline the plan builds on; the `IsEspDevice` fix directly affects auto-discovery correctness. Committing them is a separate decision (they are a teammate's merge fixes) — not part of this spec.
- Dashboard and History tabs already read from `EspReadingRepository` (real data). Only Sensors + SensorDetail use the mock service (`MockSensorDataService`, registered in `MauiProgram.cs`).
- Authorization must not depend on a network at this stage. An online ID database is future work; the design leaves a seam (`IDeviceAuthorizationService`).
- No automated test framework exists in the project; verification is on-device via logcat and UI.

## Architecture

### 1. `EspAutoSyncService` (new singleton service)

The central loop. One instance, started once at app launch, running on a background task for the app's foreground lifetime. It is the only caller of `SynchronizeAsync` after the Sync tab is deleted (the service keeps its single-flight `_syncGate` semantics).

Loop semantics — snapshot-based passes (matches the existing `ScanForDevicesAsync` API, which always stops the adapter scan when it returns):

1. Request Bluetooth permission once at startup (`RequestBluetoothPermissionAsync`). If denied, emit status and keep the banner; do NOT re-prompt (Android would re-show the dialog each pass). On every pass, re-check only `IsBluetoothEnabledAsync()` and surface its state — the loop never sits silently in "permission missing".
2. Run one scan pass: `ScanForDevicesAsync(timeout: 10 s)` (scans stop between passes; `ConnectAsync` also stops any active scan, so pass-based flow avoids adapter contention).
3. For each discovered ESP device, in order of RSSI:
   - Skip if the device is currently syncing or in its 30 s cooldown.
   - Connect to the device.
   - Read its identity via a new `IEspBluetoothService.ReadDeviceIdentityAsync(EspDeviceInfo)` method (public identity-only read, extracted from the private `ReadIdentityAsync`).
   - Ask `IDeviceAuthorizationService.IsAuthorizedAsync(identity.DeviceId)`.
     - Unauthorized → disconnect immediately, mark device `Ignored`, continue with the next device. No data transfer.
     - Authorized → proceed.
   - Run `SynchronizeAsync(device, identity, progress, token)` — a new overload that accepts the already-read identity and skips the duplicate identity read inside `RunSynchronizationAsync` (`RunSynchronizationAsync` keeps its identity-read when the overload is not used).
   - Disconnect.
   - Start a 30 s cooldown for that device. Other devices remain connectable during the cooldown.
4. Brief pause, then back to step 2. First pass starts immediately at launch.

On failure at any step (connect error, sync error, connection lost): log, mark the device `Failed`, do not crash the loop; retry on a later pass. The loop catches all exceptions from a pass.

Observable state exposed for the Scan tab (via `ObservableObject`):
- `Devices`: `ObservableCollection<EspDeviceRow>` — a new row view model (not an extension of the positional `EspDeviceInfo` record, to avoid ripples into its `with` expressions and `CreateDeviceInfo`). Each row wraps `EspDeviceInfo` and adds `AuthorizationStatus` (Authorized / Ignored / Pending), `SyncStatus` (Idle / Connecting / Syncing / Failed / Last sync), and `LastSyncAt` (DateTimeOffset?).
- `IsScanning`, `BluetoothStatusText`, `StatusMessage`.

UI-thread contract: the loop runs on a background task; all mutations of the `ObservableCollection` and `ObservableObject` state are dispatched to the UI thread (`MainThread.BeginInvokeOnMainThread`, the pattern already used in `SyncPageModel`). The page model never touches service state on a background thread.

Starts once from `App.OnStart` (or AppShell constructor). Runs for the lifetime of the app; no stop/start UI.

### 2. `IDeviceAuthorizationService` (new interface + stub)

Seam for the future online device-ID database.

```csharp
public interface IDeviceAuthorizationService
{
    Task<bool> IsAuthorizedAsync(string espDeviceId);
}
```

- `AllowAllDeviceAuthorizationService`: stub returning `true` for every device. Registered in DI.
- Future work: HTTP implementation calling the online DB keyed by the ESP identity device ID — no app changes beyond registration and a config/URL.

### 3. Scan tab → passive "Devices" list

- `ScanPage`: buttons ("Start Scan", "Stop") removed. Page becomes a read-only live list driven by `EspAutoSyncService` state:
  - Each row: device name, address, RSSI badge, status badge (Authorized / Syncing / Ignored / Failed / Last sync time).
  - Page loads data when appearing and updates via service events; the scan loop is app-wide, not tied to the page.
- `ScanPageModel`: rewritten to consume `EspAutoSyncService` instead of driving scans itself. `SelectDeviceAsync`/navigation to sync page removed.
- `DashboardPageModel` empty-state copy ("Use the Scan tab to connect to an ESP.") is stale — reworded to reference automatic discovery.

### 4. Sync tab removed

- Remove the `Sync` `ShellContent` from `AppShell.xaml`.
- Delete `SyncPage`, `SyncPageModel`, and their DI registrations.
- The manual connect/sync flow disappears entirely.

### 5. Sensors tab → real data

- New `SensorDataService` (replacing `MockSensorDataService`) implementing the existing `ISensorDataService` against `EspReadingRepository`.
- **Sensor model**: mirrors the current UI — temperature and humidity are separate sensors. Each device with readings yields two `SensorSummary` entries (one per metric). The mock had two hardcoded sensors; the real service derives them from the repository's device list (`GetDeviceIdsAsync`).
- **Stable int id mapping**: `ISensorDataService` is keyed by `int sensorId`, but readings are keyed by string deviceId. The service derives `sensorId` deterministically (FNV-1a 32-bit hash of `"{deviceId}|temp"` / `"{deviceId}|hum"`), stable across launches. Reverse lookup (`GetSensorAsync(int)`, `GetReadingsAsync(int)`) rebuilds the mapping by iterating the repository's device list and recomputing hashes — a few devices, so this is cheap and stateless.
- Population: `Name` = device display name; `SourceDeviceId` = device id; `Type` = "Temperature" / "Humidity"; `IsOnline` = device has at least one stored reading (no recency window); `Value` = latest metric value formatted (e.g., "24.5°C", "45%").
- `GetReadingsAsync(sensorId)` returns the device's history for that single metric, ordered ascending by time (matches the mock's ordering and `SensorDetailPage` list rendering). `Value` formatted per metric; timestamp displayed as local time (`RecordedAtUtc.ToLocalTime()`, matching `DashboardPageModel`).
- Delete `MockSensorDataService`; register `SensorDataService` in `MauiProgram.cs`.
- `SensorsPage` / `SensorDetailPage` XAML unchanged — they consume `ISensorDataService`.

## Error Handling

- All loop steps wrapped in try/catch; failures recorded on the device entry and retried on a later pass. No unhandled exceptions escape the loop.
- `SynchronizeAsync` already throws typed `EspSyncException`; those messages surface in the device status.
- Bluetooth disabled / permission denied → status message shown; loop keeps retrying.
- Connection loss during transfer → handled by existing `ConnectionLost`/transfer-completion machinery; the loop treats it as a failure and retries.

## Testing & Verification

- Build: `dotnet build -f net10.0-android` (0 errors).
- On-device: install, launch, verify:
  - Devices tab populates without pressing anything; devices appear shortly after launch.
  - Readings from an authorized (all) device appear in Sensors tab, SensorDetail, History, Dashboard — no mock values.
  - Second launch after a completed sync downloads only new readings (cursor + REQUEST_AFTER); duplicates not re-inserted.
  - Bluetooth off → banner, no crash; turning it on resumes the loop.

## Out of Scope

- Actual online authorization database (stub accepts everyone; interface ready).
- Background execution when the app is not in the foreground.
- UI redesigns beyond removing the manual-scan buttons and the Sync tab.
