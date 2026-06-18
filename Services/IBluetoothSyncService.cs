using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public interface IBluetoothSyncService
{
    Task<bool> IsBluetoothEnabledAsync();
    Task EnableBluetoothAsync();
    Task<BluetoothDeviceInfo?> LocateDeviceAsync();
    Task<BluetoothDeviceInfo?> ConnectAsync(BluetoothDeviceInfo device);
    Task<BluetoothDeviceInfo?> DisconnectAsync();
}
