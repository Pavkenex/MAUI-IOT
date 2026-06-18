using MAUI_IOT.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MAUI_IOT.Services;

public sealed class MockSensorDataService : ISensorDataService
{
    private static readonly IReadOnlyList<SensorSummary> Sensors = [
        new SensorSummary(1, "Temperature Sensor", "Temperature", true, "24.5°C"),
        new SensorSummary(2, "Humidity Sensor", "Humidity", true, "45%")
    ];

    private static readonly IReadOnlyList<Reading> Readings = [
        new Reading(1, 1, "22.5\u00B0C", new DateTime(2026, 6, 18, 10, 0, 0)),
        new Reading(2, 1, "22.8\u00B0C", new DateTime(2026, 6, 18, 10, 5, 0)),
        new Reading(3, 1, "23.1\u00B0C", new DateTime(2026, 6, 18, 10, 10, 0)),
        new Reading(4, 1, "22.9\u00B0C", new DateTime(2026, 6, 18, 10, 15, 0)),
        new Reading(5, 1, "23.7\u00B0C", new DateTime(2026, 6, 18, 10, 20, 0)),
        new Reading(6, 1, "24.0\u00B0C", new DateTime(2026, 6, 18, 10, 25, 0)),
        new Reading(7, 1, "24.5\u00B0C", new DateTime(2026, 6, 18, 10, 30, 0)),
        new Reading(8, 2, "47%", new DateTime(2026, 6, 18, 10, 0, 0)),
        new Reading(9, 2, "46%", new DateTime(2026, 6, 18, 10, 5, 0)),
        new Reading(10, 2, "44%", new DateTime(2026, 6, 18, 10, 10, 0)),
        new Reading(11, 2, "43%", new DateTime(2026, 6, 18, 10, 15, 0)),
        new Reading(12, 2, "44%", new DateTime(2026, 6, 18, 10, 20, 0)),
        new Reading(13, 2, "46%", new DateTime(2026, 6, 18, 10, 25, 0)),
        new Reading(14, 2, "45%", new DateTime(2026, 6, 18, 10, 30, 0))
    ];

    public async Task<IReadOnlyList<SensorSummary>> GetSensorsAsync()
    {
        await Task.Delay(500);
        return Sensors;
    }

    public async Task<SensorSummary?> GetSensorAsync(int id)
    {
        await Task.Delay(300);
        return Sensors.FirstOrDefault(sensor => sensor.Id == id);
    }

    public async Task<IReadOnlyList<Reading>> GetReadingsAsync(int sensorId)
    {
        await Task.Delay(300);
        return Readings.Where(reading => reading.SensorId == sensorId).ToList();
    }
}
