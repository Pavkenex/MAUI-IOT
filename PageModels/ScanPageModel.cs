using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using System.Collections.ObjectModel;

namespace MAUI_IOT.PageModels;

public partial class ScanPageModel : ObservableObject
{
    private readonly IBluetoothScanService _bluetoothScanService;

    public ScanPageModel(IBluetoothScanService bluetoothScanService)
    {
        _bluetoothScanService = bluetoothScanService;
    }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isBluetoothEnabled;

    [ObservableProperty]
    private string statusMessage = "Bluetooth is disabled.";

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private ScanSession? latestSession;

    [ObservableProperty]
    private int totalSessions;

    [ObservableProperty]
    private int totalBroadcasts;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasLatestSession => LatestSession is not null;
    public bool CanStartScan => IsBluetoothEnabled && !IsBusy;
    public bool CanEnableBluetooth => !IsBluetoothEnabled && !IsBusy;

    public string BluetoothStatusText => IsBluetoothEnabled ? "Bluetooth enabled" : "Bluetooth disabled";

    public ObservableCollection<EspBroadcastReading> Broadcasts { get; } = [];

    [RelayCommand]
    private async Task LoadStatusAsync()
    {
        IsBluetoothEnabled = await _bluetoothScanService.IsBluetoothEnabledAsync();
        StatusMessage = IsBluetoothEnabled
            ? "Bluetooth is enabled. Start a scan to collect ESP broadcasts."
            : "Bluetooth is disabled. Enable Bluetooth to begin scanning.";
        await LoadHistorySummaryAsync();
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
            await _bluetoothScanService.EnableBluetoothAsync();
            IsBluetoothEnabled = true;
            StatusMessage = "Bluetooth enabled. Tap 'Start Scan' to collect ESP broadcasts.";
        });
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        if (IsBusy || !IsBluetoothEnabled)
            return;

        await RunAsync(async () =>
        {
            StatusMessage = "Scanning for nearby ESP devices...";
            LatestSession = await _bluetoothScanService.RunMockScanAsync();
            StatusMessage = $"Scan complete. Received {LatestSession.BroadcastCount} broadcasts from {LatestSession.Broadcasts.Select(b => b.DeviceUid).Distinct().Count()} devices.";
            await LoadHistorySummaryAsync();
            PopulateBroadcasts();
        });
    }

    private void PopulateBroadcasts()
    {
        Broadcasts.Clear();
        if (LatestSession is null)
            return;

        foreach (var broadcast in LatestSession.Broadcasts.OrderByDescending(b => b.ReceivedAt))
        {
            Broadcasts.Add(broadcast);
        }
    }

    private async Task LoadHistorySummaryAsync()
    {
        var sessions = await _bluetoothScanService.GetScanSessionsAsync();
        TotalSessions = sessions.Count;
        TotalBroadcasts = sessions.Sum(s => s.BroadcastCount);
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
            ErrorMessage = $"Scan error: {ex.Message}";
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
        OnPropertyChanged(nameof(HasLatestSession));
        OnPropertyChanged(nameof(CanStartScan));
        OnPropertyChanged(nameof(CanEnableBluetooth));
        OnPropertyChanged(nameof(BluetoothStatusText));
    }
}
