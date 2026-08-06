namespace MAUI_IOT.Models;

public enum EspSyncStatus
{
    Completed,
    Cancelled,
    Failed,
    Disconnected,
}

public sealed record EspSyncResult(
    string DeviceId,
    uint BootSessionId,
    uint EspOldestReadingId,
    uint EspLatestReadingId,
    uint RequestedAfterReadingId,
    uint LastPersistedReadingId,
    int ReceivedCount,
    int NewlyPersistedCount,
    int DuplicateCount,
    bool HasDataGap,
    EspSyncStatus Status,
    string? ErrorMessage);
