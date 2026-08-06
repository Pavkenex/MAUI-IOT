# Design: Auto-Discovery & Sync with Device Authorization

Date: 2026-08-07
Status: Approved

## Goal

Replace the manual BLE flow (Scan tab with Start Scan button, Sync tab with Connect/Sync buttons) with automatic discovery: while the app is open, nearby ESP32 DHT22 loggers are scanned continuously; any device whose ID is authorized (currently: all devices, via a stub) is connected to and its readings are downloaded automatically. The Sensors tab must show real readings from SQLite instead of mock data.

## Background / Constraints

- App: MAUI .NET 10, `Plugin.BLE` 3.2.1, `sqlite-net-pcl` 1.9.172.
- Real readings already land in SQLite via `EspReadingRepository`; `EspBluetoothService` already implements connect/sync with cursor-based delta sync (`REQUEST_AFTER`), idempotent inserts, boot-session detection, and a 120 s synchronization timeout.
- Dashboard and History tabs already read from `EspReadingRepository` (real data). Only Sensors + SensorDetail use the mock service (`MockSensorDataService`, registered in `MauiProgram.cs`).
- Authorization must not depend on a network at this stage. An online ID database is future work; the design leaves a seam (`IDeviceAuthorizationService`).
- No automated test framework exists in the project; verification is on-device via logcat and UI.

## Architecture

### 1. `EspAutoSyncService` (new singleton service)

The central loop. One instance, started once at app launch, running on a background task for the app's foreground lifetime.

Behavior:

- Continuously scans for ESP devices (no fixed per-scan timeout; scan runs until stopped).
- For each discovered ESP device, in order of RSSI:
  1. If device is already connected, synced, or in cooldown for this device — skip.
  2. Connect to the device.
  3. Read the device identity.
  4. Ask `IDeviceAuthorizationService.IsAuthorizedAsync(identity.DeviceId)`.
     - Unauthorized → disconnect immediately, mark device `Ignored`, continue scanning. No data transfer.
     - Authorized → proceed.
  5. Run `SynchronizeAsync` (existing protocol flow).
  6. Disconnect.
  7. Start a 30 s cooldown for that device (no reconnect/sync until it expires). Other devices remain connectable during the cooldown.
- On failure at any step (connect error, sync error, connection lost): log, mark the device `Failed`, do not crash the loop; retry on a later pass.
- If Bluetooth is off or permissions missing: emit status, keep retrying on subsequent passes.

Observable state exposed for the Scan tab (via `ObservableObject`):
- `Devices`: live list of discovered ESP devices with `AuthorizationStatus`, `SyncStatus`, and `LastSyncAt`.
- `IsScanning`, `BluetoothStatusText`, `StatusMessage`.

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

### 4. Sync tab removed

- Remove the `Sync` `ShellContent` from `AppShell.xaml`.
- Delete `SyncPage`, `SyncPageModel`, and their DI registrations.
- The manual connect/sync flow disappears entirely.

### 5. Sensors tab → real data

- New `SensorDataService` (replacing `MockSensorDataService`) implementing the existing `ISensorDataService` against `EspReadingRepository`:
  - `GetSensorsAsync`: one `SensorSummary` per device that has readings (derived from repository device list); value shows latest temperature/humidity.
  - `GetSensorAsync(int)`: sensor summary by id.
  - `GetReadingsAsync(int sensorId)`: reading history for the device from SQLite.
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
