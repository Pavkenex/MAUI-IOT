using SQLite;

namespace MAUI_IOT.Models;

[Table("esp_readings")]
public sealed class EspReading
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("idx_esp_reading_unique", 1, Unique = true)]
    public string DeviceId { get; set; } = string.Empty;

    [Indexed("idx_esp_reading_unique", 2, Unique = true)]
    public uint BootSessionId { get; set; }

    [Indexed("idx_esp_reading_unique", 3, Unique = true)]
    public uint ReadingId { get; set; }

    public uint ElapsedSeconds { get; set; }

    public double TemperatureCelsius { get; set; }

    public double HumidityPercent { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }

    [Indexed]
    public bool IsUploaded { get; set; }

    public DateTimeOffset? UploadedAtUtc { get; set; }

    [Ignore]
    public string? DeviceName { get; set; }
}
