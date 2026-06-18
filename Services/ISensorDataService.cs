using MAUI_IOT.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MAUI_IOT.Services;

public interface ISensorDataService
{
    Task<IReadOnlyList<SensorSummary>> GetSensorsAsync();
    Task<SensorSummary?> GetSensorAsync(int sensorId);
    Task<IReadOnlyList<Reading>> GetReadingsAsync(int sensorId);
}
