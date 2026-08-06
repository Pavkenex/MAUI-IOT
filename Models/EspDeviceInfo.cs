namespace MAUI_IOT.Models;

public sealed record EspDeviceInfo(
    string Id,
    string Name,
    string Address,
    int Rssi,
    string? DeviceId,
    bool IsConnected);
