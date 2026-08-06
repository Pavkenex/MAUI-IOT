using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using Microsoft.Maui.ApplicationModel;
using System.Collections.ObjectModel;

namespace MAUI_IOT.PageModels;

[QueryProperty(nameof(DeviceId), "deviceId")]
public partial class SyncPageModel : ObservableObject
{
    private readonly IEspBluetoothService _bluetoothService;
    private readonly IEspReadingRepository _repository;
    private CancellationTokenSource? _syncCts;

    public SyncPageModel(IEspBluetoothService bluetoothService, IEspReadingRepository repository)
    {
        _bluetoothService = bluetoothService;
        _repository = repository;
        _bluetoothService.ConnectionLost += OnConnectionLost;
    }

    private void OnConnectionLost(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = false;
            if (Device is not null)
            {
                Device = Device with { IsConnected = false };
            }

            StatusMessage = "Connection to the ESP was lost unexpectedly.";
            NotifyStateChanged();
        });
    }

    [ObservableProperty]
    private string? deviceId;

    [ObservableProperty]
    private EspDeviceInfo? device;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool isSyncing;

    [ObservableProperty]
    private string statusMessage = "No device selected.";

    [ObservableProperty]
    private string progressText = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private EspSyncResult? lastSyncResult;

    public ObservableCollection<EspReadingView> RecentReadings { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasDevice => Device is not null;
    public bool HasSyncResult => LastSyncResult is not null;
    public bool HasDataGap => LastSyncResult?.HasDataGap == true;
    public bool CanConnect => HasDevice && !IsConnected && !IsBusy && !IsSyncing;
    public bool CanSync => IsConnected && !IsSyncing && !IsBusy;
    public bool CanCancelSync => IsSyncing;
    public bool CanDisconnect => IsConnected && !IsSyncing && !IsBusy;
    public string DeviceStatusText => IsConnected ? "Connected" : HasDevice ? "Not connected" : "No device";
    public string BluetoothStatusText => "Bluetooth";

    public string ResultSummaryText
    {
        get
        {
            if (LastSyncResult is null)
            {
                return string.Empty;
            }

            var result = LastSyncResult;
            var summary = $"Received {result.ReceivedCount}, newly saved {result.NewlyPersistedCount}, duplicates {result.DuplicateCount}. " +
                          $"Last persisted reading ID: {result.LastPersistedReadingId}.";
            if (result.HasDataGap)
            {
                summary += " A history gap was detected (older readings were overwritten on the ESP).";
            }

            return summary;
        }
    }

    partial void OnDeviceIdChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Device = null;
            return;
        }

        Device = _bluetoothService.GetScannedDevice(value);
        if (Device is null)
        {
            StatusMessage = "Selected device is no longer in the scan list. Run a scan again.";
        }
        else
        {
            StatusMessage = $"Selected {Device.Name} ({Device.Address}). Connect to continue.";
        }
    }

    [RelayCommand]
    private async Task LoadStatusAsync()
    {
        IsConnected = _bluetoothService.IsConnected;
        if (Device is not null && _bluetoothService.ConnectedDevice?.Id == Device.Id)
        {
            Device = Device with { IsConnected = IsConnected };
        }

        await LoadRecentReadingsAsync();
        NotifyStateChanged();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (Device is null || IsBusy || IsSyncing)
        {
            return;
        }

        await RunAsync(async () =>
        {
            StatusMessage = $"Connecting to {Device.Name}...";
            await _bluetoothService.ConnectAsync(Device);
            IsConnected = true;
            Device = Device with { IsConnected = true };
            StatusMessage = $"Connected to {Device.Name}. Tap Sync to download readings.";
        });
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (Device is null || !IsConnected || IsSyncing)
        {
            return;
        }

        try
        {
            IsSyncing = true;
            IsBusy = true;
            ErrorMessage = null;
            NotifyStateChanged();

            _syncCts = new CancellationTokenSource();
            var progress = new Progress<EspSyncProgress>(UpdateProgress);

            LastSyncResult = await _bluetoothService.SynchronizeAsync(Device, progress, _syncCts.Token);

            if (LastSyncResult.Status == EspSyncStatus.Completed)
            {
                StatusMessage = $"Synchronization completed. {LastSyncResult.NewlyPersistedCount} new reading(s) saved.";
                if (Device is not null)
                {
                    Device = Device with { DeviceId = LastSyncResult.DeviceId };
                }
            }
            else
            {
                StatusMessage = $"Synchronization ended with status {LastSyncResult.Status}.";
            }

            ProgressText = string.Empty;
            await LoadRecentReadingsAsync();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Synchronization cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sync error: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
            IsBusy = false;
            _syncCts = null;
            NotifyStateChanged();
        }
    }

    [RelayCommand]
    private void CancelSync()
    {
        _syncCts?.Cancel();
        StatusMessage = "Cancelling synchronization...";
        NotifyStateChanged();
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        if (!IsConnected || IsSyncing)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _bluetoothService.DisconnectAsync();
            IsConnected = false;
            if (Device is not null)
            {
                Device = Device with { IsConnected = false };
            }

            StatusMessage = "Disconnected from the ESP.";
        });
    }

    private void UpdateProgress(EspSyncProgress progress)
    {
        ProgressText = progress.Stage switch
        {
            EspSyncStage.Connecting => "Connecting...",
            EspSyncStage.ReadingDeviceInfo => "Reading device information...",
            EspSyncStage.Subscribing => "Preparing data channel...",
            EspSyncStage.Transferring => $"Downloading readings: {progress.PersistedCount} saved, {progress.ReceivedCount} received...",
            EspSyncStage.Finalizing => "Finalizing synchronization...",
            _ => "Working...",
        };
    }

    private async Task LoadRecentReadingsAsync()
    {
        RecentReadings.Clear();
        if (Device is null)
        {
            return;
        }

        IReadOnlyList<EspReading> readings;
        if (!string.IsNullOrEmpty(Device.DeviceId))
        {
            readings = await _repository.GetReadingsAsync(deviceId: Device.DeviceId, limit: 50);
        }
        else
        {
            readings = await _repository.GetReadingsAsync(limit: 50);
        }

        foreach (var reading in readings)
        {
            RecentReadings.Add(EspReadingView.From(reading));
        }
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
            ErrorMessage = $"Error: {ex.Message}";
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
        OnPropertyChanged(nameof(HasDevice));
        OnPropertyChanged(nameof(HasSyncResult));
        OnPropertyChanged(nameof(HasDataGap));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CanSync));
        OnPropertyChanged(nameof(CanCancelSync));
        OnPropertyChanged(nameof(CanDisconnect));
        OnPropertyChanged(nameof(DeviceStatusText));
        OnPropertyChanged(nameof(ResultSummaryText));
    }
}
