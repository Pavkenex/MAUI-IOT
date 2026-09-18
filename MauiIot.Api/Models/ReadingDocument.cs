namespace MauiIot.Api.Models;

public sealed class ReadingDocument
{
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string? DeviceName { get; set; }

    public uint BootSessionId { get; set; }

    public uint ReadingId { get; set; }

    public uint ElapsedSeconds { get; set; }

    public double TemperatureCelsius { get; set; }

    public double HumidityPercent { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
