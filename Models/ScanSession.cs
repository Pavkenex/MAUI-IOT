namespace MAUI_IOT.Models;

public sealed record ScanSession(
    Guid Id,
    string Name,
    DateTime StartedAt,
    DateTime? EndedAt,
    IReadOnlyList<RoutePoint> RoutePoints,
    IReadOnlyList<EspBroadcastReading> Broadcasts)
{
    public int BroadcastCount => Broadcasts.Count;

    public TimeSpan Duration => (EndedAt ?? DateTime.Now) - StartedAt;
}
