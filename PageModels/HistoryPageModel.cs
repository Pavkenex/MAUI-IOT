using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using System.Collections.ObjectModel;

namespace MAUI_IOT.PageModels;

public partial class HistoryPageModel : ObservableObject
{
    private readonly IEspReadingRepository _repository;

    public HistoryPageModel(IEspReadingRepository repository)
    {
        _repository = repository;
    }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty]
    private bool hasReadings;

    [ObservableProperty]
    private int totalReadings;

    [ObservableProperty]
    private int totalDevices;

    [ObservableProperty]
    private int pendingUploads;

    [ObservableProperty]
    private string storageSummary = "No readings stored yet. Readings are downloaded automatically from nearby ESPs.";

    public ObservableCollection<EspReadingView> Readings { get; } = [];

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

            var readings = await _repository.GetReadingsAsync(limit: 200);
            Readings.Clear();

            HasReadings = readings.Count > 0;

            foreach (var reading in readings)
            {
                Readings.Add(EspReadingView.From(reading));
            }

            var deviceIds = await _repository.GetDeviceIdsAsync();
            TotalReadings = await _repository.GetReadingCountAsync();
            TotalDevices = deviceIds.Count;
            PendingUploads = await _repository.GetPendingUploadCountAsync();
            StorageSummary = TotalReadings == 0
                ? "No readings stored yet. Readings are downloaded automatically from nearby ESPs."
                : $"{TotalReadings} reading(s) stored across {TotalDevices} ESP device(s).";
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
