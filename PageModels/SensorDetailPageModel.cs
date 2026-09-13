using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MAUI_IOT.PageModels
{
    public partial class SensorDetailPageModel : ObservableObject, IQueryAttributable
    {
        private readonly ISensorDataService _sensorDataService;
        private int _currentSensorId;

        public SensorDetailPageModel(ISensorDataService sensorDataService)
        {
            _sensorDataService = sensorDataService;
        }

        [ObservableProperty]
        private SensorSummary? sensor;
        public ObservableCollection<Reading> Readings { get; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMapLocation))]
        private Reading? selectedReading;

        [ObservableProperty]
        private HtmlWebViewSource? mapSource;

        public bool HasMapLocation => SelectedReading?.HasLocation == true;

        [ObservableProperty]
        private bool isBusy;
        [ObservableProperty]
        private bool isRefreshing;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        private string? errorMessage;

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool HasReadings => Readings.Count > 0;
        public bool IsEmpty => !HasReadings && !HasError && !IsBusy;

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasReadings));
            OnPropertyChanged(nameof(IsEmpty));
        }

        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (!query.TryGetValue("id", out var value) || value is null)
            {
                ErrorMessage = "No sensor id provided.";
                return;
            }
            if (!int.TryParse(value.ToString(), out var id))
            {
                ErrorMessage = "Invalid sensor id provided.";
                return;
            }

            _currentSensorId = id;
            await LoadSensorAsync();
        }

        [RelayCommand]
        private async Task LoadSensorAsync()
        {
            if (IsBusy)
                return;

            if (_currentSensorId == 0)
            {
                ErrorMessage = "No sensor id provided.";
                NotifyStateChanged();
                return;
            }

            try
            {
                IsBusy = true;
                ErrorMessage = null;
                Readings.Clear();
                NotifyStateChanged();

                Sensor = await _sensorDataService.GetSensorAsync(_currentSensorId);

                if (Sensor is null)
                {
                    ErrorMessage = $"Sensor with id {_currentSensorId} not found.";
                    NotifyStateChanged();
                    return;
                }

                var readings = await _sensorDataService.GetReadingsAsync(_currentSensorId);
                for (var index = readings.Count - 1; index >= 0; index--)
                {
                    Readings.Add(readings[index]);
                }
                SelectedReading = Readings.FirstOrDefault(r => r.HasLocation);
                NotifyStateChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading sensor: {ex.Message}";
                NotifyStateChanged();
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
                NotifyStateChanged();
            }
        }

        partial void OnSelectedReadingChanged(Reading? value)
        {
            MapSource = BuildMapView(value);
        }

        private static HtmlWebViewSource? BuildMapView(Reading? reading)
        {
            if (reading is null || !reading.HasLocation)
            {
                return null;
            }

            var invariant = CultureInfo.InvariantCulture;
            var markerJson = JsonSerializer.Serialize(new
            {
                lat = reading.Latitude!.Value,
                lon = reading.Longitude!.Value,
                value = reading.Value,
                time = reading.Timestamp.ToString("yyyy-MM-dd HH:mm", invariant),
                coords = reading.LocationText,
            });

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
                    .popup-value { font-weight: 700; color: #071A3A; font-size: 15px; margin-bottom: 2px; }
                    .popup-meta { color: #6E7890; font-size: 11px; line-height: 1.5; }
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
                    var p = {{markerJson}};
                    var map = L.map('map', { zoomControl: false, attributionControl: true });
                    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                        maxZoom: 19
                    }).addTo(map);
                    L.marker([p.lat, p.lon]).addTo(map).bindPopup(
                        '<div class="popup-value">' + esc(p.value) + '</div>' +
                        '<div class="popup-meta">Recorded: ' + esc(p.time) + '<br>' + esc(p.coords) + '</div>'
                    );
                    map.setView([p.lat, p.lon], 16);
                    </script>
                    </body>
                    </html>
                    """
            };
        }

    }
}
