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
    public partial class DevicesPageModel : ObservableObject
    {
        private readonly IDeviceDataService _deviceDataService;

        public DevicesPageModel(IDeviceDataService deviceDataService)
        {
            _deviceDataService = deviceDataService;
        }
        public ObservableCollection<DeviceSummary> Devices { get; } = [];
        [ObservableProperty]
        private bool isBusy;
        [ObservableProperty]
        private string? errorMessage;
        [RelayCommand]

        private async Task LoadDevicesAsync()
        {
            if (IsBusy)
                return;
            try
            {
                IsBusy = true;
                ErrorMessage = null;
                Devices.Clear();
                var devices = await _deviceDataService.GetDevicesAsync();
                foreach (var device in devices)
                {
                    Devices.Add(device);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading devices: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }


    }
}
