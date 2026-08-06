using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using System.Collections.ObjectModel;

namespace MAUI_IOT.PageModels;

public partial class ScanPageModel : ObservableObject
{
    private readonly IEspBluetoothService _bluetoothService;
    private CancellationTokenSource? _scanCts;

    public ScanPageModel(IEspBluetoothService bluetoothService)
    {
        _bluetoothService = bluetoothService;
    }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isBluetoothEnabled;

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private string statusMessage = "Checking Bluetooth state...";

    [ObservableProperty]
    private string? errorMessage;

    public ObservableCollection<EspDeviceInfo> Devices { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasDevices => Devices.Count > 0;
    public bool CanStartScan => IsBluetoothEnabled && !IsBusy && !IsScanning;
    public bool CanStopScan => IsScanning;
    public string BluetoothStatusText => IsBluetoothEnabled ? "Bluetooth enabled" : "Bluetooth disabled";

    [RelayCommand]
    private async Task LoadStatusAsync()
    {
        var granted = await _bluetoothService.RequestBluetoothPermissionAsync();
        IsBluetoothEnabled = await _bluetoothService.IsBluetoothEnabledAsync();

        if (!granted)
        {
            ErrorMessage = "Bluetooth permission denied. Grant it in Settings to scan for ESP devices.";
            NotifyStateChanged();
            return;
        }

        if (!IsBluetoothEnabled)
        {
            StatusMessage = "Bluetooth is disabled. Enable it in system settings, then tap Start Scan.";
        }
        else
        {
            StatusMessage = Devices.Count > 0
                ? $"Ready. {Devices.Count} ESP device(s) discovered."
                : "Bluetooth is enabled. Tap Start Scan to discover ESP32 DHT22 devices.";
        }

        ErrorMessage = null;
        NotifyStateChanged();
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        if (IsBusy || !IsBluetoothEnabled || IsScanning)
        {
            return;
        }

        try
        {
            IsBusy = true;
            IsScanning = true;
            ErrorMessage = null;
            NotifyStateChanged();

            Devices.Clear();
            StatusMessage = "Scanning for ESP32 DHT22 devices...";

            _scanCts = new CancellationTokenSource();
            var progress = new Progress<EspDeviceInfo>(device =>
            {
                var index = IndexOf(device.Id);
                if (index < 0)
                {
                    Devices.Add(device);
                }
                else
                {
                    Devices[index] = device;
                }
            });

            var devices = await _bluetoothService.ScanForDevicesAsync(
                progress,
                cancellationToken: _scanCts.Token);

            StatusMessage = devices.Count == 0
                ? "Scan finished. No DHT22 ESP devices found nearby."
                : $"Scan finished. Found {devices.Count} ESP device(s).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan stopped.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            IsBusy = false;
            _scanCts = null;
            NotifyStateChanged();
        }
    }

    [RelayCommand]
    private async Task StopScanAsync()
    {
        _scanCts?.Cancel();
        await _bluetoothService.StopScanAsync();
        StatusMessage = "Stopping scan...";
        NotifyStateChanged();
    }

    [RelayCommand]
    private async Task SelectDeviceAsync(EspDeviceInfo device)
    {
        if (IsBusy)
        {
            return;
        }

        await Shell.Current.GoToAsync($"sync?deviceId={device.Id}");
    }

    private int IndexOf(string id)
    {
        for (var i = 0; i < Devices.Count; i++)
        {
            if (Devices[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(CanStartScan));
        OnPropertyChanged(nameof(CanStopScan));
        OnPropertyChanged(nameof(BluetoothStatusText));
    }
}
