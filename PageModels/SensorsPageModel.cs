using CommunityToolkit.Mvvm.ComponentModel;
using MAUI_IOT.Services;
using MAUI_IOT.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.Input;

namespace MAUI_IOT.PageModels
{
    public partial class SensorsPageModel : ObservableObject
    {
        private readonly ISensorDataService _sensorDataService;

        public SensorsPageModel(ISensorDataService sensorDataService)
        {
            _sensorDataService = sensorDataService;
        }
        public ObservableCollection<SensorSummary> Sensors { get; } = [];
        [ObservableProperty]
        private bool isBusy;
        [ObservableProperty]
        private bool isRefreshing;
        [ObservableProperty]
        private string? errorMessage;
        [ObservableProperty]
        private string lastUpdated = "Last updated --";

        public bool HasSensors => Sensors.Count > 0;
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool IsEmpty => !HasSensors && !HasError && !IsBusy;


        [RelayCommand]
        private async Task LoadSensorsAsync()
        {
            if (IsBusy)
                return;
            try
            {
                IsBusy = true;
                ErrorMessage = null;
                NotifySensorStateChanged();
                Sensors.Clear();
                var sensors = await _sensorDataService.GetSensorsAsync();
                foreach (var sensor in sensors)
                {
                    Sensors.Add(sensor);
                }
                LastUpdated = $"Last updated {DateTime.Now:HH:mm}";
                NotifySensorStateChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading sensors: {ex.Message}";
                NotifySensorStateChanged();
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
                NotifySensorStateChanged();
            }
        }

        private void NotifySensorStateChanged()
        {
            OnPropertyChanged(nameof(HasSensors));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasError));
        }
        [RelayCommand]
        private async Task OpenSensorAsync(SensorSummary sensor)
        {
            await Shell.Current.GoToAsync($"sensor?id={sensor.Id}");
        }

    }
}
