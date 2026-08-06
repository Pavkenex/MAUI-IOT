using SQLite;

namespace MAUI_IOT.Models;

[Table("esp_devices")]
public sealed class EspDeviceRecord
{
    [PrimaryKey]
    public string DeviceId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset LastSeenAtUtc { get; set; }
}
