namespace MAUI_IOT.Models;

public sealed record EspReadingView(
    string DeviceId,
    uint ReadingId,
    uint BootSessionId,
    double TemperatureCelsius,
    double HumidityPercent,
    DateTime RecordedAtLocal,
    bool IsUploaded)
{
    public static EspReadingView From(EspReading reading)
    {
        return new EspReadingView(
            reading.DeviceId,
            reading.ReadingId,
            reading.BootSessionId,
            reading.TemperatureCelsius,
            reading.HumidityPercent,
            reading.RecordedAtUtc.ToLocalTime().DateTime,
            reading.IsUploaded);
    }
}
