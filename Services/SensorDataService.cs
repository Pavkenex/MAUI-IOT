using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public sealed class SensorDataService : ISensorDataService
{
    private const int ReadingsHistoryLimit = 200;

    private readonly IEspReadingRepository _repository;

    public SensorDataService(IEspReadingRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<SensorSummary>> GetSensorsAsync()
    {
        var devices = await _repository.GetDevicesAsync();
        var sensors = new List<SensorSummary>();
        foreach (var device in devices)
        {
            var name = string.IsNullOrWhiteSpace(device.Name)
                ? $"DHT22 {ShortDeviceId(device.DeviceId)}"
                : device.Name;
            var latest = await _repository.GetLatestReadingAsync(device.DeviceId);
            var isOnline = latest is not null;

            sensors.Add(new SensorSummary(
                SensorIdHasher.ForDevice(device.DeviceId, isTemperature: true),
                device.DeviceId,
                name,
                "Temperature",
                isOnline,
                latest is null ? "--" : $"{latest.TemperatureCelsius:F1}°C"));
            sensors.Add(new SensorSummary(
                SensorIdHasher.ForDevice(device.DeviceId, isTemperature: false),
                device.DeviceId,
                name,
                "Humidity",
                isOnline,
                latest is null ? "--" : $"{latest.HumidityPercent:F1}%"));
        }

        return sensors.OrderBy(s => s.Name).ThenBy(s => s.Type).ToList();
    }

    public async Task<SensorSummary?> GetSensorAsync(int sensorId)
    {
        var sensors = await GetSensorsAsync();
        return sensors.FirstOrDefault(s => s.Id == sensorId);
    }

    public async Task<IReadOnlyList<Reading>> GetReadingsAsync(int sensorId)
    {
        var resolved = await ResolveSensorAsync(sensorId);
        if (resolved is null)
        {
            return [];
        }

        var (deviceId, isTemperature) = resolved.Value;
        var readings = await _repository.GetReadingsAsync(deviceId: deviceId, limit: ReadingsHistoryLimit);
        var ordered = readings.OrderBy(r => r.RecordedAtUtc).ToList();

        var result = new List<Reading>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            var reading = ordered[index];
            result.Add(new Reading(
                index + 1,
                sensorId,
                isTemperature
                    ? $"{reading.TemperatureCelsius:F1}°C"
                    : $"{reading.HumidityPercent:F1}%",
                reading.RecordedAtUtc.ToLocalTime().DateTime,
                reading.Latitude,
                reading.Longitude));
        }

        return result;
    }

    private async Task<(string DeviceId, bool IsTemperature)?> ResolveSensorAsync(int sensorId)
    {
        var devices = await _repository.GetDevicesAsync();
        foreach (var device in devices)
        {
            if (SensorIdHasher.ForDevice(device.DeviceId, isTemperature: true) == sensorId)
            {
                return (device.DeviceId, true);
            }

            if (SensorIdHasher.ForDevice(device.DeviceId, isTemperature: false) == sensorId)
            {
                return (device.DeviceId, false);
            }
        }

        return null;
    }

    private static string ShortDeviceId(string deviceId)
    {
        return deviceId.Length >= 8 ? deviceId[^8..].ToUpperInvariant() : deviceId;
    }
}
