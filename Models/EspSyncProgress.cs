namespace MAUI_IOT.Models;

public enum EspSyncStage
{
    Connecting,
    ReadingDeviceInfo,
    Subscribing,
    Transferring,
    Finalizing,
}

public sealed record EspSyncProgress(
    EspSyncStage Stage,
    int ReceivedCount,
    int PersistedCount,
    int DuplicateCount,
    uint LastReadingId);
