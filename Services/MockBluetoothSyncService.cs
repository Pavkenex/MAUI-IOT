using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public sealed class MockBluetoothSyncService : IBluetoothSyncService
{
    private readonly ISensorDataService _sensorDataService;
    private bool _isBluetoothEnabled;
    private BluetoothDeviceInfo? _locatedDevice;

    public MockBluetoothSyncService(ISensorDataService sensorDataService)
    {
        _sensorDataService = sensorDataService;
    }

    public Task<bool> IsBluetoothEnabledAsync()
    {
        return Task.FromResult(_isBluetoothEnabled);
    }

    public async Task EnableBluetoothAsync()
    {
        await Task.Delay(500);
        _isBluetoothEnabled = true;
    }

    public async Task<BluetoothDeviceInfo?> LocateDeviceAsync()
    {
        if (!_isBluetoothEnabled)
            return null;

        await Task.Delay(900);

        var sensors = await _sensorDataService.GetSensorsAsync();
        var onboardSensors = sensors
            .Where(sensor => sensor.SourceDeviceId == MockSensorDataService.AtmosMonitorDeviceId)
            .ToList();

        _locatedDevice = new BluetoothDeviceInfo(
            MockSensorDataService.AtmosMonitorDeviceId,
            "Atmos Monitor BLE",
            "BLE-7A:42:19",
            -48,
            onboardSensors,
            false);

        return _locatedDevice;
    }

    public async Task<BluetoothDeviceInfo?> ConnectAsync(BluetoothDeviceInfo device)
    {
        if (!_isBluetoothEnabled)
            return null;

        await Task.Delay(700);

        _locatedDevice = device with { IsConnected = true };
        return _locatedDevice;
    }

    public async Task<BluetoothDeviceInfo?> DisconnectAsync()
    {
        await Task.Delay(300);

        if (_locatedDevice is null)
            return null;

        _locatedDevice = _locatedDevice with { IsConnected = false };
        return _locatedDevice;
    }
}
