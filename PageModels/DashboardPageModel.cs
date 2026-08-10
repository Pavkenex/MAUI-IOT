using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using System.Collections.ObjectModel;
using System.Globalization;

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
        private string latestDeviceId = "--";

        [ObservableProperty]
        private double? latestLatitude;

        [ObservableProperty]
        private double? latestLongitude;

        public bool HasLatestLocation => LatestLatitude is not null && LatestLongitude is not null;

        [ObservableProperty]
        private HtmlWebViewSource? mapSource;

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
            var deviceIds = await _repository.GetDeviceIdsAsync();

            if (deviceIds.Count == 0)
            {
                LatestDeviceId = "--";
                Temperature = "--";
                Humidity = "--";
                TrendTemperature = "--";
                TrendHumidity = "--";
                TrendBars.Clear();
                ClearLatestLocation();
                return;
            }

            var deviceId = deviceIds.First();
            var readings = await _repository.GetReadingsAsync(deviceId, limit: 7);
            var latest = readings.FirstOrDefault();

            if (latest is null)
            {
                ClearLatestLocation();
                return;
            }

            LatestDeviceId = latest.DeviceId;
            Temperature = $"{latest.TemperatureCelsius:F1}°C";
            Humidity = $"{latest.HumidityPercent:F1}%";
            TrendTemperature = Temperature;
            TrendHumidity = Humidity;

            UpdateLatestLocation(latest);
            LoadTrendBars(readings.OrderBy(r => r.RecordedAtUtc).ToList());
        }

        private void UpdateLatestLocation(EspReading latest)
        {
            LatestLatitude = latest.Latitude;
            LatestLongitude = latest.Longitude;
            OnPropertyChanged(nameof(HasLatestLocation));

            MapSource = HasLatestLocation
                ? BuildMapSource(LatestLatitude!.Value, LatestLongitude!.Value)
                : null;
        }

        private void ClearLatestLocation()
        {
            LatestLatitude = null;
            LatestLongitude = null;
            OnPropertyChanged(nameof(HasLatestLocation));
            MapSource = null;
        }

        private static HtmlWebViewSource BuildMapSource(double latitude, double longitude)
        {
            var invariant = CultureInfo.InvariantCulture;
            var lat = latitude.ToString("F6", invariant);
            var lon = longitude.ToString("F6", invariant);

            return new HtmlWebViewSource
            {
                Html = $$"""
                    <!DOCTYPE html>
                    <html>
                    <head>
                    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
                    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css">
                    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
                    <style>
                    html, body { margin: 0; padding: 0; height: 100%; }
                    #map { height: 100%; width: 100%; background: #DDE3ED; }
                    </style>
                    </head>
                    <body>
                    <div id="map"></div>
                    <script>
                    var map = L.map('map', { zoomControl: false, attributionControl: true }).setView([{{lat}}, {{lon}}], 16);
                    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                        maxZoom: 19
                    }).addTo(map);
                    L.marker([{{lat}}, {{lon}}]).addTo(map);
                    </script>
                    </body>
                    </html>
                    """
            };
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
