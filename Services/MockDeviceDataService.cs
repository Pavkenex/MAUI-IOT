using MAUI_IOT.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MAUI_IOT.Services
{
    public sealed class MockDeviceDataService : IDeviceDataService
    {
        public async Task<IReadOnlyList<DeviceSummary>> GetDevicesAsync()
        {
            await Task.Delay(500); // Simulate network delay
            return [
                new DeviceSummary(1, "Temperature Sensor", "Sensor", true, "22.5°C"),
                new DeviceSummary(2, "Humidity Sensor", "Sensor", false, "45%"),
                new DeviceSummary(3, "Light Sensor", "Sensor", true, "300 lux")
            ];
        }
    }
}
