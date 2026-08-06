using CommunityToolkit.Mvvm.ComponentModel;

namespace MAUI_IOT.Models;

public enum EspDeviceAuthorizationStatus
{
    Pending,
    Authorized,
    Ignored,
}

public enum EspDeviceSyncStatus
{
    Idle,
    Connecting,
    Syncing,
    Failed,
}

public sealed partial class EspDeviceRow : ObservableObject
{
    public required string Id { get; init; }

    [ObservableProperty]
    private string name = "Unknown ESP";

    [ObservableProperty]
    private string address = string.Empty;

    [ObservableProperty]
    private int rssi;

    [ObservableProperty]
    private EspDeviceAuthorizationStatus authorizationStatus;

    [ObservableProperty]
    private EspDeviceSyncStatus syncStatus;

    [ObservableProperty]
    private DateTimeOffset? lastSyncAt;

    [ObservableProperty]
    private string? syncError;

    public string AuthText => AuthorizationStatus switch
    {
        EspDeviceAuthorizationStatus.Authorized => "Authorized",
        EspDeviceAuthorizationStatus.Ignored => "Ignored",
        _ => "Pending",
    };

    public string StatusText
    {
        get
        {
            if (SyncStatus == EspDeviceSyncStatus.Connecting)
            {
                return "Connecting...";
            }

            if (SyncStatus == EspDeviceSyncStatus.Syncing)
            {
                return "Syncing...";
            }

            if (SyncStatus == EspDeviceSyncStatus.Failed)
            {
                return $"Failed: {SyncError ?? "unknown error"}";
            }

            if (LastSyncAt is not null)
            {
                return $"Synced {LastSyncAt.Value.ToLocalTime():HH:mm}";
            }

            return "Idle";
        }
    }

    partial void OnAuthorizationStatusChanged(EspDeviceAuthorizationStatus value)
    {
        OnPropertyChanged(nameof(AuthText));
    }

    partial void OnSyncStatusChanged(EspDeviceSyncStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnLastSyncAtChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnSyncErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(StatusText));
    }

    public static EspDeviceRow From(EspDeviceInfo device)
    {
        return new EspDeviceRow
        {
            Id = device.Id,
            Name = device.Name,
            Address = device.Address,
            Rssi = device.Rssi,
        };
    }
}
