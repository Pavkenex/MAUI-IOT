using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MAUI_IOT.PageModels
{
    public partial class DashboardPageModel:ObservableObject
    {
        private readonly ISensorDataService _sensorService;

        public DashboardPageModel(ISensorDataService sensorService)
        {
            _sensorService = sensorService;
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
        private string gpsTitle = "Primary GPS Data";

        [ObservableProperty]
        private string latitude = "34.0522° N";

        [ObservableProperty]
        private string longitude = "118.2437° W";

        [ObservableProperty]
        private string altitude = "284m AMSL";

        [ObservableProperty]
        private string signalStatus = "Signal: Locked";

        [ObservableProperty]
        private string trendTitle = "Atmospheric Trends";

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
                LastUpdated = $"Last updated {DateTime.Now:HH:mm}";

            }
            catch(Exception ex)
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

            var temperatureValues = temperatureReadings.Select(reading => ParseNumber(reading.Value)).ToList();
            var humidityValues = humidityReadings.Select(reading => ParseNumber(reading.Value)).ToList();
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
                .Where(character => char.IsDigit(character) || character == '.' || character == ',' || character == '-')
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
