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
        private readonly ISensorDataService _sensorService;
        private readonly IBluetoothScanService _scanService;

        public DashboardPageModel(
            ISensorDataService sensorService,
            IBluetoothScanService scanService)
        {
            _sensorService = sensorService;
            _scanService = scanService;
        }

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string? errorMessage;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        // Demo sensor readings (from mock sensor service)
        [ObservableProperty]
        private string temperature = "--";
        [ObservableProperty]
        private string humidity = "--";
        [ObservableProperty]
        private string lastUpdated = "Last updated --";

        // Scan context
        [ObservableProperty]
        private int totalScanSessions;

        [ObservableProperty]
        private int totalBroadcasts;

        [ObservableProperty]
        private int uniqueDevices;

        [ObservableProperty]
        private string scanSummary = "No scans yet";

        // Latest broadcast data (multi-device)
        [ObservableProperty]
        private string latestDeviceLabel = "--";

        [ObservableProperty]
        private string latestDeviceUid = "--";

        [ObservableProperty]
        private string latestTemperature = "--";

        [ObservableProperty]
        private string latestHumidity = "--";

        [ObservableProperty]
        private string latestSignal = "--";

        // GPS data from latest broadcast
        [ObservableProperty]
        private string gpsTitle = "Latest ESP Device GPS";

        [ObservableProperty]
        private string latitude = "--";

        [ObservableProperty]
        private string longitude = "--";

        [ObservableProperty]
        private string altitude = "N/A";

        [ObservableProperty]
        private string signalStatus = "Signal: --";

        // Trend
        [ObservableProperty]
        private string trendTitle = "Sensor Trends";

        [ObservableProperty]
        private string trendSubtitle = "Latest 7 measurements";

        [ObservableProperty]
        private string selectedTrendRange = "Live";

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

                // Load sensor data (existing mock demo data)
                await LoadSensorDataAsync();

                // Load scan data
                await LoadScanDataAsync();

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

        private async Task LoadSensorDataAsync()
        {
            var sensors = await _sensorService.GetSensorsAsync();
            var temperatureSensor = FindSensor(sensors, "Temperature");
            var humiditySensor = FindSensor(sensors, "Humidity");
            var temperatureReadings = temperatureSensor is null
                ? []
                : LatestReadings(await _sensorService.GetReadingsAsync(temperatureSensor.Id));
            var humidityReadings = humiditySensor is null
                ? []
                : LatestReadings(await _sensorService.GetReadingsAsync(humiditySensor.Id));

            Temperature = temperatureReadings.LastOrDefault()?.Value ?? temperatureSensor?.LatestReading ?? "--";
            Humidity = humidityReadings.LastOrDefault()?.Value ?? humiditySensor?.LatestReading ?? "--";
            TrendTemperature = Temperature;
            TrendHumidity = Humidity;
            LoadTrendBars(temperatureReadings, humidityReadings);
        }

        private async Task LoadScanDataAsync()
        {
            var sessions = await _scanService.GetScanSessionsAsync();
            TotalScanSessions = sessions.Count;
            TotalBroadcasts = sessions.Sum(s => s.BroadcastCount);
            UniqueDevices = sessions
                .SelectMany(s => s.Broadcasts)
                .Select(b => b.DeviceUid)
                .Distinct()
                .Count();

            if (TotalBroadcasts > 0)
            {
                ScanSummary = $"{TotalScanSessions} session(s), {UniqueDevices} ESP device(s), {TotalBroadcasts} broadcast(s)";

                // Take latest broadcast from most recent session
                var latestSession = sessions.OrderByDescending(s => s.StartedAt).First();
                var latestBroadcast = latestSession.Broadcasts
                    .OrderByDescending(b => b.ReceivedAt)
                    .First();

                LatestDeviceLabel = latestBroadcast.DeviceLabel;
                LatestDeviceUid = latestBroadcast.DeviceUid;
                LatestTemperature = $"{latestBroadcast.Temperature:F1}°C";
                LatestHumidity = $"{latestBroadcast.Humidity:F1}%";
                LatestSignal = $"{latestBroadcast.SignalStrength} dBm";

                Latitude = $"{latestBroadcast.Latitude:F6}° N";
                Longitude = $"{latestBroadcast.Longitude:F6}° W";
                SignalStatus = latestBroadcast.IsAuthenticated
                    ? $"Trusted ({latestBroadcast.DeviceUid})"
                    : $"Unknown ({latestBroadcast.DeviceUid})";

                GpsTitle = $"{latestBroadcast.DeviceUid} GPS";
            }
            else
            {
                ScanSummary = "No scans yet. Go to the Scan tab and start a BLE scan.";
                LatestDeviceLabel = "--";
                LatestDeviceUid = "--";
                LatestTemperature = "--";
                LatestHumidity = "--";
                LatestSignal = "--";
                Latitude = "--";
                Longitude = "--";
                SignalStatus = "Signal: --";
                GpsTitle = "ESP Device GPS";
            }
        }

        private static SensorSummary? FindSensor(IReadOnlyList<SensorSummary> sensors, string namePart)
        {
            return sensors.FirstOrDefault(sensor =>
                sensor.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase) ||
                sensor.Type.Contains(namePart, StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyList<Reading> LatestReadings(IReadOnlyList<Reading> readings)
        {
            return readings
                .OrderBy(reading => reading.Timestamp)
                .TakeLast(7)
                .ToList();
        }

        private void LoadTrendBars(IReadOnlyList<Reading> temperatureReadings, IReadOnlyList<Reading> humidityReadings)
        {
            TrendBars.Clear();

            var temperatureValues = temperatureReadings.Select(r => ParseNumber(r.Value)).ToList();
            var humidityValues = humidityReadings.Select(r => ParseNumber(r.Value)).ToList();
            var count = Math.Max(temperatureValues.Count, humidityValues.Count);

            for (var index = 0; index < count; index++)
            {
                var label = index < temperatureReadings.Count
                    ? temperatureReadings[index].Timestamp.ToString("HH:mm")
                    : humidityReadings[index].Timestamp.ToString("HH:mm");

                TrendBars.Add(new TrendBar(
                    NormalizeHeight(temperatureValues, index),
                    NormalizeHeight(humidityValues, index),
                    label));
            }
        }

        private static double ParseNumber(string value)
        {
            var numericText = new string(value
                .Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-')
                .ToArray())
                .Replace(',', '.');

            return double.TryParse(numericText, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? number
                : 0;
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
