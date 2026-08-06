using SQLite;

namespace MAUI_IOT.Models;

[Table("esp_sync_cursors")]
public sealed class EspSyncCursor
{
    [PrimaryKey]
    public string DeviceId { get; set; } = string.Empty;

    public uint BootSessionId { get; set; }

    public uint LastPersistedReadingId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
