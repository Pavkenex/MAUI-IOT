using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public sealed class SensorDataService : ISensorDataService
{
    private const int ReadingsHistoryLimit = 200;
    private const int MaxDevices = 100;

    private readonly IEspReadingRepository _repository;

    public SensorDataService(IEspReadingRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<SensorSummary>> GetSensorsAsync()
    {
        var devices = await _repository.GetDevicesAsync();
        var latestPerDevice = await _repository.GetLatestReadingsPerDeviceAsync(MaxDevices);

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
        {
            names[device.DeviceId] = device.Name;
        }

        var latestByDevice = new Dictionary<string, EspReading>(StringComparer.OrdinalIgnoreCase);
        foreach (var reading in latestPerDevice)
        {
            latestByDevice[reading.DeviceId] = reading;
        }

        var deviceIds = names.Keys
            .Union(latestByDevice.Keys, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sensors = new List<SensorSummary>(deviceIds.Count * 2);
        foreach (var deviceId in deviceIds)
        {
            var storedName = names.TryGetValue(deviceId, out var value) ? value : null;
            var name = DeviceNameFormatter.ForDevice(deviceId, storedName);
            latestByDevice.TryGetValue(deviceId, out var latest);
            var isOnline = latest is not null;

            sensors.Add(new SensorSummary(
                SensorIdHasher.ForDevice(deviceId, isTemperature: true),
                deviceId,
                name,
                "Temperature",
                isOnline,
                latest is null ? "--" : $"{latest.TemperatureCelsius:F1}°C"));
            sensors.Add(new SensorSummary(
                SensorIdHasher.ForDevice(deviceId, isTemperature: false),
                deviceId,
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
        var latestPerDevice = await _repository.GetLatestReadingsPerDeviceAsync(MaxDevices);
        var deviceIds = devices.Select(d => d.DeviceId)
            .Concat(latestPerDevice.Select(r => r.DeviceId))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var deviceId in deviceIds)
        {
            if (SensorIdHasher.ForDevice(deviceId, isTemperature: true) == sensorId)
            {
                return (deviceId, true);
            }

            if (SensorIdHasher.ForDevice(deviceId, isTemperature: false) == sensorId)
            {
                return (deviceId, false);
            }
        }

        return null;
    }

}
