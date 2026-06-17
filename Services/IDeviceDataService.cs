using MAUI_IOT.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MAUI_IOT.Services
{
    public interface IDeviceDataService
    {
        Task<IReadOnlyList<DeviceSummary>> GetDevicesAsync();
    }
}
