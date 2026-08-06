using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using System.Collections.ObjectModel;

namespace MAUI_IOT.PageModels
{
    public partial class DashboardPageModel : ObservableObject
    {
        private readonly IEspReadingRepository _repository;

        public DashboardPageModel(IEspReadingRepository repository)
        {
            _repository = repository;
        }

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string? errorMessage;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        [ObservableProperty]
        private string temperature = "--";
        [ObservableProperty]
        private string humidity = "--";
        [ObservableProperty]
        private string lastUpdated = "Last updated --";

        [ObservableProperty]
        private int totalReadings;

        [ObservableProperty]
        private int totalDevices;

        [ObservableProperty]
        private int pendingUploads;

        [ObservableProperty]
        private string scanSummary = "No readings stored yet. Readings are downloaded automatically from nearby ESPs.";

        [ObservableProperty]
        private string latestDeviceId = "--";

        [ObservableProperty]
        private string trendTitle = "Recent Measurements";

        [ObservableProperty]
        private string trendSubtitle = "Latest 7 readings";

        [ObservableProperty]
        private string selectedTrendRange = "Last 7";

        [ObservableProperty]
        private string trendTemperature = "--";

        [ObservableProperty]
        private string trendHumidity = "--";

        public ObservableCollection<TrendBar> TrendBars { get; } = [];

        [RelayCommand]
        private async Task LoadDashboardAsync()
        {
            if (IsBusy)
                return;
            try
            {
                IsBusy = true;
                ErrorMessage = null;
                OnPropertyChanged(nameof(HasError));

                await LoadStoredDataAsync();

                LastUpdated = $"Last updated {DateTime.Now:HH:mm}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading dashboard: {ex.Message}";
                OnPropertyChanged(nameof(HasError));
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
            }
        }

        private async Task LoadStoredDataAsync()
        {
            TotalReadings = await _repository.GetReadingCountAsync();
            PendingUploads = await _repository.GetPendingUploadCountAsync();
            var deviceIds = await _repository.GetDeviceIdsAsync();
            TotalDevices = deviceIds.Count;

            if (TotalReadings == 0 || deviceIds.Count == 0)
            {
                ScanSummary = "No readings stored yet. Readings are downloaded automatically from nearby ESPs.";
                LatestDeviceId = "--";
                Temperature = "--";
                Humidity = "--";
                TrendTemperature = "--";
                TrendHumidity = "--";
                TrendBars.Clear();
                return;
            }

            ScanSummary = $"{TotalReadings} reading(s) stored, {PendingUploads} pending cloud upload, {TotalDevices} ESP device(s).";

            var deviceId = deviceIds.First();
            var readings = await _repository.GetReadingsAsync(deviceId, limit: 7);
            var latest = readings.FirstOrDefault();

            if (latest is null)
            {
                return;
            }

            LatestDeviceId = latest.DeviceId;
            Temperature = $"{latest.TemperatureCelsius:F1}°C";
            Humidity = $"{latest.HumidityPercent:F1}%";
            TrendTemperature = Temperature;
            TrendHumidity = Humidity;

            LoadTrendBars(readings.OrderBy(r => r.RecordedAtUtc).ToList());
        }

        private void LoadTrendBars(IReadOnlyList<EspReading> readings)
        {
            TrendBars.Clear();

            if (readings.Count == 0)
            {
                return;
            }

            var temperatureValues = readings.Select(r => r.TemperatureCelsius).ToList();
            var humidityValues = readings.Select(r => r.HumidityPercent).ToList();

            for (var index = 0; index < readings.Count; index++)
            {
                TrendBars.Add(new TrendBar(
                    NormalizeHeight(temperatureValues, index),
                    NormalizeHeight(humidityValues, index),
                    readings[index].RecordedAtUtc.ToLocalTime().DateTime.ToString("HH:mm")));
            }
        }

        private static double NormalizeHeight(IReadOnlyList<double> values, int index)
        {
            const double minHeight = 24;
            const double maxHeight = 110;

            if (index >= values.Count)
                return minHeight;

            var min = values.Min();
            var max = values.Max();

            if (Math.Abs(max - min) < 0.001)
                return (minHeight + maxHeight) / 2;

            return minHeight + ((values[index] - min) / (max - min) * (maxHeight - minHeight));
        }
    }

    public sealed record TrendBar(double TemperatureHeight, double HumidityHeight, string Label);
}
