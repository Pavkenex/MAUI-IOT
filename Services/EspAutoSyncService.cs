using CommunityToolkit.Mvvm.ComponentModel;
using MAUI_IOT.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MAUI_IOT.Services;

public sealed partial class EspAutoSyncService : ObservableObject
{
    private static readonly TimeSpan ScanPassTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PassDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CooldownAfterSync = TimeSpan.FromSeconds(30);

    private readonly IEspBluetoothService _bluetooth;
    private readonly IEspReadingRepository _repository;
    private readonly IDeviceAuthorizationService _authorization;
    private readonly ILogger<EspAutoSyncService> _logger;
    private readonly Dictionary<string, DateTimeOffset> _cooldowns = new();
    private Task? _loop;
    private bool _permissionRequested;
    private int _lastReportedProgress;

    public EspAutoSyncService(
        IEspBluetoothService bluetooth,
        IEspReadingRepository repository,
        IDeviceAuthorizationService authorization,
        ILogger<EspAutoSyncService> logger)
    {
        _bluetooth = bluetooth;
        _repository = repository;
        _authorization = authorization;
        _logger = logger;
    }

    public ObservableCollection<EspDeviceRow> Devices { get; } = [];

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private string bluetoothStatusText = "Checking Bluetooth...";

    [ObservableProperty]
    private string statusMessage = "Auto-discovery is starting...";

    [ObservableProperty]
    private string? errorMessage;

    public void Start()
    {
        if (_loop is null)
        {
            _loop = RunLoopAsync();
        }
    }

    private async Task RunLoopAsync()
    {
        while (true)
        {
            try
            {
                await RunPassAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-discovery pass failed.");
                await OnUiAsync(() => ErrorMessage = $"Auto-discovery error: {ex.Message}");
            }

            await Task.Delay(PassDelay);
        }
    }

    private async Task RunPassAsync()
    {
        if (!_permissionRequested)
        {
            _permissionRequested = true;
            var granted = await _bluetooth.RequestBluetoothPermissionAsync();
            if (!granted)
            {
                await OnUiAsync(() =>
                {
                    ErrorMessage = "Bluetooth permission denied. Grant it in Settings to auto-sync ESP devices.";
                    StatusMessage = "Auto-discovery is waiting for Bluetooth permission.";
                });
                return;
            }
        }

        if (!await _bluetooth.IsBluetoothEnabledAsync())
        {
            await OnUiAsync(() =>
            {
                BluetoothStatusText = "Bluetooth disabled";
                StatusMessage = "Bluetooth is disabled. Enable it to auto-sync nearby ESPs.";
            });
            return;
        }

        await OnUiAsync(() =>
        {
            BluetoothStatusText = "Bluetooth enabled";
            ErrorMessage = null;
            StatusMessage = "Scanning for nearby ESPs...";
            IsScanning = true;
        });

        IReadOnlyList<EspDeviceInfo> found;
        try
        {
            var progress = new Progress<EspDeviceInfo>(UpsertDevice);
            found = await _bluetooth.ScanForDevicesAsync(progress, ScanPassTimeout);
        }
        finally
        {
            await OnUiAsync(() => IsScanning = false);
        }

        var seenIds = new HashSet<string>();
        foreach (var device in found.OrderByDescending(d => d.Rssi))
        {
            seenIds.Add(device.Id);
            await HandleDeviceAsync(device);
        }

        await PruneDevicesAsync(seenIds);
    }

    private async Task PruneDevicesAsync(HashSet<string> seenIds)
    {
        await OnUiAsync(() =>
        {
            for (var index = Devices.Count - 1; index >= 0; index--)
            {
                var row = Devices[index];
                if (!seenIds.Contains(row.Id) &&
                    row.SyncStatus is not EspDeviceSyncStatus.Connecting and not EspDeviceSyncStatus.Syncing)
                {
                    Devices.RemoveAt(index);
                }
            }
        });
    }

    private void UpsertDevice(EspDeviceInfo device)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => UpsertDevice(device));
            return;
        }

        var row = Devices.FirstOrDefault(r => r.Id == device.Id);
        if (row is null)
        {
            Devices.Add(EspDeviceRow.From(device));
        }
        else
        {
            row.Name = device.Name;
            row.Rssi = device.Rssi;
        }
    }

    private async Task HandleDeviceAsync(EspDeviceInfo device)
    {
        var row = await FindRowAsync(device.Id);
        if (row is null)
        {
            return;
        }

        if (row.SyncStatus is EspDeviceSyncStatus.Connecting or EspDeviceSyncStatus.Syncing)
        {
            return;
        }

        if (_cooldowns.TryGetValue(device.Id, out var cooldownUntil) && DateTimeOffset.UtcNow < cooldownUntil)
        {
            return;
        }

        try
        {
            await OnUiAsync(() =>
            {
                row.SyncStatus = EspDeviceSyncStatus.Connecting;
                row.SyncError = null;
                StatusMessage = $"Connecting to {device.Name}...";
            });

            await _bluetooth.ConnectAsync(device);

            var identity = await _bluetooth.ReadDeviceIdentityAsync(device);

            if (!await _authorization.IsAuthorizedAsync(identity.DeviceId))
            {
                _logger.LogInformation("Device {Device} is not authorized; ignoring.", device.Name);
                await OnUiAsync(() =>
                {
                    row.AuthorizationStatus = EspDeviceAuthorizationStatus.Ignored;
                    row.SyncStatus = EspDeviceSyncStatus.Idle;
                    StatusMessage = $"Ignored {device.Name} (not authorized).";
                });
                return;
            }

            await _repository.SaveDeviceAsync(new EspDeviceRecord
            {
                DeviceId = identity.DeviceId,
                Name = device.Name,
                LastSeenAtUtc = DateTimeOffset.UtcNow,
            });

            await OnUiAsync(() =>
            {
                row.AuthorizationStatus = EspDeviceAuthorizationStatus.Authorized;
                row.SyncStatus = EspDeviceSyncStatus.Syncing;
                _lastReportedProgress = 0;
                StatusMessage = $"Syncing {device.Name}...";
            });

            var progress = new Progress<EspSyncProgress>(p =>
            {
                if (p.PersistedCount <= _lastReportedProgress)
                {
                    return;
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _lastReportedProgress = p.PersistedCount;
                    StatusMessage = $"Syncing {device.Name}: {p.PersistedCount} new reading(s)...";
                });
            });

            await _bluetooth.SynchronizeAsync(device, identity, progress);

            _cooldowns[device.Id] = DateTimeOffset.UtcNow + CooldownAfterSync;
            await OnUiAsync(() =>
            {
                row.SyncStatus = EspDeviceSyncStatus.Idle;
                row.LastSyncAt = DateTimeOffset.Now;
                StatusMessage = $"Synced {device.Name}.";
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sync failed for {Device}.", device.Name);
            await OnUiAsync(() =>
            {
                row.SyncStatus = EspDeviceSyncStatus.Failed;
                row.SyncError = ex.Message;
                StatusMessage = $"Sync failed for {device.Name}: {ex.Message}";
            });
        }
        finally
        {
            try
            {
                await _bluetooth.DisconnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Disconnect after sync failed for {Device}.", device.Name);
            }
        }
    }

    private async Task<EspDeviceRow?> FindRowAsync(string id)
    {
        EspDeviceRow? row = null;
        await OnUiAsync(() => row = Devices.FirstOrDefault(r => r.Id == id));
        return row;
    }

    private static async Task OnUiAsync(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(action);
    }
}
