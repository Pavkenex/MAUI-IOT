using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;

namespace MAUI_IOT.PageModels;

public partial class SyncPageModel : ObservableObject
{
    private readonly IBluetoothSyncService _bluetoothSyncService;

    public SyncPageModel(IBluetoothSyncService bluetoothSyncService)
    {
        _bluetoothSyncService = bluetoothSyncService;
    }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isBluetoothEnabled;

    [ObservableProperty]
    private BluetoothDeviceInfo? locatedDevice;

    [ObservableProperty]
    private string statusMessage = "Bluetooth is disabled.";

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasLocatedDevice => LocatedDevice is not null;
    public bool IsConnected => LocatedDevice?.IsConnected == true;
    public bool CanEnableBluetooth => !IsBluetoothEnabled && !IsBusy;
    public bool CanLocateDevice => IsBluetoothEnabled && !IsConnected && !IsBusy;
    public bool CanConnectDevice => HasLocatedDevice && !IsConnected && !IsBusy;
    public bool CanDisconnectDevice => IsConnected && !IsBusy;
    public string BluetoothStatusText => IsBluetoothEnabled ? "Bluetooth enabled" : "Bluetooth disabled";
    public string DeviceStatusText => IsConnected ? "Connected" : HasLocatedDevice ? "Located" : "No device located";

    [RelayCommand]
    private async Task LoadStatusAsync()
    {
        IsBluetoothEnabled = await _bluetoothSyncService.IsBluetoothEnabledAsync();
        StatusMessage = IsBluetoothEnabled
            ? "Bluetooth is enabled. Locate the IoT device to continue."
            : "Bluetooth is disabled.";
        NotifyStateChanged();
    }

    [RelayCommand]
    private async Task EnableBluetoothAsync()
    {
        if (IsBusy || IsBluetoothEnabled)
            return;

        await RunAsync(async () =>
        {
            StatusMessage = "Enabling Bluetooth...";
            await _bluetoothSyncService.EnableBluetoothAsync();
            IsBluetoothEnabled = true;
            StatusMessage = "Bluetooth enabled. You can now locate the IoT device.";
        });
    }

    [RelayCommand]
    private async Task LocateDeviceAsync()
    {
        if (IsBusy || !IsBluetoothEnabled)
            return;

        await RunAsync(async () =>
        {
            StatusMessage = "Scanning for nearby IoT devices...";
            LocatedDevice = await _bluetoothSyncService.LocateDeviceAsync();
            StatusMessage = LocatedDevice is null
                ? "No mock device found. Enable Bluetooth and try again."
                : $"Located {LocatedDevice.Name}.";
        });
    }

    [RelayCommand]
    private async Task ConnectDeviceAsync()
    {
        if (IsBusy || LocatedDevice is null || IsConnected)
            return;

        await RunAsync(async () =>
        {
            StatusMessage = $"Connecting to {LocatedDevice.Name}...";
            LocatedDevice = await _bluetoothSyncService.ConnectAsync(LocatedDevice);
            StatusMessage = LocatedDevice?.IsConnected == true
                ? $"Connected to {LocatedDevice.Name}. Ready to sync readings."
                : "Could not connect to the mock device.";
        });
    }

    [RelayCommand]
    private async Task DisconnectDeviceAsync()
    {
        if (IsBusy || !IsConnected)
            return;

        await RunAsync(async () =>
        {
            StatusMessage = $"Disconnecting from {LocatedDevice?.Name}...";
            LocatedDevice = await _bluetoothSyncService.DisconnectAsync();
            StatusMessage = "Device disconnected.";
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            NotifyStateChanged();

            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sync error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasLocatedDevice));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(CanEnableBluetooth));
        OnPropertyChanged(nameof(CanLocateDevice));
        OnPropertyChanged(nameof(CanConnectDevice));
        OnPropertyChanged(nameof(CanDisconnectDevice));
        OnPropertyChanged(nameof(BluetoothStatusText));
        OnPropertyChanged(nameof(DeviceStatusText));
    }
}
