using CommunityToolkit.Mvvm.ComponentModel;
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
        private string? errorMessage;

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

            await LoadSensorAsync(id);
        }

        private async Task LoadSensorAsync(int id)
        {
            try
            {
                IsBusy = true;
                ErrorMessage = null;
                Readings.Clear();

                Sensor = await _sensorDataService.GetSensorAsync(id);
                
                if (Sensor is null)
                {
                    ErrorMessage = $"Sensor with id {id} not found.";
                    return;
                }

                var readings = await _sensorDataService.GetReadingsAsync(id);
                foreach (var reading in readings)
                {
                    Readings.Add(reading);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading sensor: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

    }
}
