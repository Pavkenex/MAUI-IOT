using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

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
                foreach (var reading in readings)
                {
                    Readings.Add(reading);
                }
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

    }
}
