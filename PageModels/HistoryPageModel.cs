using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using System.Collections.ObjectModel;

namespace MAUI_IOT.PageModels;

public partial class HistoryPageModel : ObservableObject
{
    private readonly IBluetoothScanService _bluetoothScanService;

    public HistoryPageModel(IBluetoothScanService bluetoothScanService)
    {
        _bluetoothScanService = bluetoothScanService;
    }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public ObservableCollection<ScanSessionSummary> Sessions { get; } = [];

    public ObservableCollection<EspBroadcastReading> RecentBroadcasts { get; } = [];

    [ObservableProperty]
    private bool hasSessions;

    [ObservableProperty]
    private int totalBroadcasts;

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            OnPropertyChanged(nameof(HasError));

            var sessions = await _bluetoothScanService.GetScanSessionsAsync();
            Sessions.Clear();
            RecentBroadcasts.Clear();

            HasSessions = sessions.Count > 0;
            TotalBroadcasts = sessions.Sum(s => s.BroadcastCount);

            foreach (var session in sessions)
            {
                var deviceCount = session.Broadcasts.Select(b => b.DeviceUid).Distinct().Count();
                var latestTime = session.Broadcasts.Count > 0
                    ? session.Broadcasts.Max(b => b.ReceivedAt)
                    : session.StartedAt;

                Sessions.Add(new ScanSessionSummary(
                    session.Id,
                    session.Name,
                    session.StartedAt,
                    latestTime,
                    session.BroadcastCount,
                    deviceCount));
            }

            // Load recent broadcasts across all sessions
            var allBroadcasts = sessions
                .SelectMany(s => s.Broadcasts)
                .OrderByDescending(b => b.ReceivedAt)
                .Take(20)
                .ToList();

            foreach (var broadcast in allBroadcasts)
            {
                RecentBroadcasts.Add(broadcast);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading history: {ex.Message}";
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed record ScanSessionSummary(
    Guid Id,
    string Name,
    DateTime StartedAt,
    DateTime LatestBroadcastAt,
    int BroadcastCount,
    int DeviceCount);
