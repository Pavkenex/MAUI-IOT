using MAUI_IOT.Models;
using MAUI_IOT.Protocol;

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

    Task<EspDeviceIdentity> ReadDeviceIdentityAsync(EspDeviceInfo device, CancellationToken cancellationToken = default);

    Task<EspSyncResult> SynchronizeAsync(
        EspDeviceInfo device,
        EspDeviceIdentity identity,
        IProgress<EspSyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
