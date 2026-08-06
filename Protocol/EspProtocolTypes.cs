namespace MAUI_IOT.Protocol;

public sealed class EspProtocolException : Exception
{
    public EspProtocolException(string message)
        : base(message)
    {
    }

    public EspProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed record EspDeviceIdentity(
    byte Version,
    string DeviceId,
    uint BootSessionId,
    ushort ReadingIntervalSeconds,
    ushort ReadingCapacity);

public sealed record EspReadingRange(
    uint BootSessionId,
    uint OldestReadingId,
    uint LatestReadingId,
    ushort StoredReadingCount);

public sealed record EspReadingMeta(
    byte Version,
    string DeviceId,
    uint BootSessionId,
    uint ReadingId);

public sealed record EspReadingValues(
    byte Version,
    uint BootSessionId,
    uint ReadingId,
    uint ElapsedSeconds,
    double TemperatureCelsius,
    double HumidityPercent);

public sealed record EspTransferEnd(
    byte Version,
    uint BootSessionId,
    uint LastSentReadingId,
    uint LatestReadingId,
    uint OldestReadingId);

public sealed record EspErrorInfo(
    byte Version,
    EspProtocol.EspErrorCode ErrorCode,
    uint BootSessionId,
    uint OldestReadingId,
    uint LatestReadingId);
