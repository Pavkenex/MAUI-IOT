namespace MAUI_IOT.Protocol;

public static class EspTimestampCalculator
{
    public static DateTimeOffset RecordedAtUtc(
        DateTimeOffset phoneAnchorUtc,
        uint espAnchorUptimeSeconds,
        uint readingElapsedSeconds)
    {
        var offsetSeconds = (long)readingElapsedSeconds - espAnchorUptimeSeconds;
        return phoneAnchorUtc.AddSeconds(offsetSeconds);
    }
}
