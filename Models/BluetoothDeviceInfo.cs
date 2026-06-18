namespace MAUI_IOT.Models;

public sealed record BluetoothDeviceInfo(
    string Id,
    string Name,
    string Address,
    int SignalStrength,
    IReadOnlyList<SensorSummary> Sensors,
    bool IsConnected);
