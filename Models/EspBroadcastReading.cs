namespace MAUI_IOT.Models;

public sealed record EspBroadcastReading(
    Guid Id,
    Guid SessionId,
    string DeviceUid,
    string DeviceLabel,
    bool IsAuthenticated,
    double Temperature,
    double Humidity,
    double Latitude,
    double Longitude,
    int SignalStrength,
    DateTime ReceivedAt,
    string RawPayload);
