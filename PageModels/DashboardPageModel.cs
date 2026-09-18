using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using Microsoft.Maui.ApplicationModel;
using System.Globalization;
using System.Text.Json;

namespace MAUI_IOT.PageModels
{
    public partial class DashboardPageModel : ObservableObject
    {
        private const int MapDeviceLimit = 10;

        private readonly IEspReadingRepository _repository;
        private readonly IAuthService _auth;

        public DashboardPageModel(IEspReadingRepository repository, IAuthService auth)
        {
            _repository = repository;
            _auth = auth;
            _auth.SessionChanged += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshAccount);
            RefreshAccount();
        }

        [ObservableProperty]
        private string accountName = "Not signed in";

        public bool IsSignedIn => _auth.HasValidSession;

        private void RefreshAccount()
        {
            AccountName = _auth.Username ?? "Not signed in";
            OnPropertyChanged(nameof(IsSignedIn));
        }

        [RelayCommand]
        private async Task LogoutAsync()
        {
            await _auth.LogoutAsync();
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("login");
            }
        }

        [RelayCommand]
        private async Task SignInAsync()
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("login");
            }
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
        private string latestDeviceName = "--";

        [ObservableProperty]
        private int latestSensorId;

        [ObservableProperty]
        private bool hasLatestLocation;

        [ObservableProperty]
        private HtmlWebViewSource? mapSource;

        [ObservableProperty]
        private string trendTitle = "Recent Measurements";

        [ObservableProperty]
        private string trendSubtitle = "Latest 7 readings · all devices";

        public TrendChartModel Trend { get; } = new();

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
                Trend.ClearSelection();
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
            var readingsNewestFirst = await _repository.GetReadingsAsync(limit: 7);
            var devices = await _repository.GetDevicesAsync();
            var deviceNames = devices
                .GroupBy(d => d.DeviceId)
                .ToDictionary(g => g.Key, g => g.First().Name);

            var latest = readingsNewestFirst.FirstOrDefault();

            if (latest is null)
            {
                LatestDeviceId = "--";
                LatestDeviceName = "--";
                LatestSensorId = 0;
                Temperature = "--";
                Humidity = "--";
                Trend.Load([], deviceNames);
                ClearMap();
                return;
            }

            var deviceId = latest.DeviceId;
            var deviceName = DeviceNameFormatter.ForDevice(
                deviceId,
                deviceNames.TryGetValue(deviceId, out var name) ? name : null);

            LatestDeviceId = deviceId;
            LatestDeviceName = deviceName;
            LatestSensorId = SensorIdHasher.ForDevice(deviceId, isTemperature: true);
            Temperature = $"{latest.TemperatureCelsius:F1}°C";
            Humidity = $"{latest.HumidityPercent:F1}%";

            await UpdateMapAsync(deviceNames);
            Trend.Load(readingsNewestFirst.OrderBy(r => r.RecordedAtUtc).ToList(), deviceNames);
        }

        private async Task UpdateMapAsync(IReadOnlyDictionary<string, string> names)
        {
            var located = await _repository.GetLastLocatedReadingsPerDeviceAsync(MapDeviceLimit);

            if (located.Count == 0)
            {
                ClearMap();
                return;
            }

            var points = located.Select(reading => new MapPoint(
                reading.Latitude!.Value,
                reading.Longitude!.Value,
                DeviceNameFormatter.ForDevice(
                    reading.DeviceId,
                    names.TryGetValue(reading.DeviceId, out var name) ? name : null),
                reading.DeviceId,
                $"{reading.TemperatureCelsius:F1}°C",
                $"{reading.HumidityPercent:F1}%",
                reading.RecordedAtUtc.ToLocalTime().DateTime,
                SensorIdHasher.ForDevice(reading.DeviceId, isTemperature: true))).ToList();

            MapSource = BuildMapSource(points);
            HasLatestLocation = true;
        }

        private void ClearMap()
        {
            HasLatestLocation = false;
            MapSource = null;
        }

        private static HtmlWebViewSource BuildMapSource(IReadOnlyList<MapPoint> points)
        {
            var invariant = CultureInfo.InvariantCulture;
            var markers = points.Select(point => new
            {
                lat = point.Latitude,
                lon = point.Longitude,
                name = point.DeviceName,
                id = point.DeviceId,
                temp = point.Temperature,
                hum = point.Humidity,
                time = point.RecordedAtLocal.ToString("yyyy-MM-dd HH:mm", invariant),
                sensorId = point.SensorId,
            });
            var markersJson = JsonSerializer.Serialize(markers);

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
                    .leaflet-popup-content-wrapper { border-radius: 10px; }
                    .popup-device { font-weight: 700; color: #071A3A; font-size: 13px; margin-bottom: 2px; }
                    .popup-meta { color: #6E7890; font-size: 11px; line-height: 1.5; }
                    .popup-link { display: block; margin-top: 8px; color: #0B4FA3; font-weight: 700; font-size: 12px; text-decoration: none; }
                    </style>
                    </head>
                    <body>
                    <div id="map"></div>
                    <script>
                    function esc(value) {
                        return String(value).replace(/[&<>"']/g, function (c) {
                            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
                        });
                    }
                    var points = {{markersJson}};
                    var map = L.map('map', { zoomControl: false, attributionControl: true });
                    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                        maxZoom: 19
                    }).addTo(map);
                    var bounds = [];
                    points.forEach(function (p) {
                        L.marker([p.lat, p.lon]).addTo(map).bindPopup(
                            '<div class="popup-device">' + esc(p.name) + '</div>' +
                            '<div class="popup-meta">' + esc(p.id) + '<br>' + esc(p.temp) + ' &middot; ' + esc(p.hum) + '<br>Recorded: ' + esc(p.time) + '</div>' +
                            '<a class="popup-link" href="app://sensor/' + encodeURIComponent(p.sensorId) + '">View sensor details</a>');
                        bounds.push([p.lat, p.lon]);
                    });
                    if (bounds.length === 1) {
                        map.setView(bounds[0], 16);
                    } else {
                        map.fitBounds(bounds, { padding: [30, 30] });
                    }
                    </script>
                    </body>
                    </html>
                    """
            };
        }
    }

    public sealed record MapPoint(
        double Latitude,
        double Longitude,
        string DeviceName,
        string DeviceId,
        string Temperature,
        string Humidity,
        DateTime RecordedAtLocal,
        int SensorId);
}
