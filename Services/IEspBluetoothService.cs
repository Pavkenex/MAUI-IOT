using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public interface IEspBluetoothService
{
    event EventHandler? ConnectionLost;

    bool IsConnected { get; }

    bool IsScanning { get; }

    EspDeviceInfo? ConnectedDevice { get; }

    Task<bool> RequestBluetoothPermissionAsync();

    Task<bool> IsBluetoothEnabledAsync();

    Task<IReadOnlyList<EspDeviceInfo>> ScanForDevicesAsync(
        IProgress<EspDeviceInfo>? progress = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    Task StopScanAsync();

    EspDeviceInfo? GetScannedDevice(string id);

    Task ConnectAsync(EspDeviceInfo device, CancellationToken cancellationToken = default);

    Task DisconnectAsync();

    Task<EspSyncResult> SynchronizeAsync(
        EspDeviceInfo device,
        IProgress<EspSyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
