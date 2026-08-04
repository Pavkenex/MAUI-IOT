using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public sealed class MockBluetoothScanService : IBluetoothScanService
{
    private bool _isBluetoothEnabled;
    private readonly List<ScanSession> _sessions = [];

    /// <summary>
    /// Known authenticated ESP device UIDs.
    /// </summary>
    public static readonly IReadOnlySet<string> AuthenticatedUids = new HashSet<string>
    {
        "esp-a001", "esp-a002", "esp-a003", "esp-a004"
    };

    /// <summary>
    /// Predefined mock ESP devices with labels.
    /// </summary>
    private static readonly IReadOnlyList<(string Uid, string Label, bool Authenticated)> KnownDevices =
    [
        ("esp-a001", "Ja sam paradajz1", true),
        ("esp-a002", "Temperatura kutija", true),
        ("esp-a003", "Vlažnost polje", true),
        ("esp-a004", "Staklenik jug", true),
        ("esp-u001", "Nepoznati uredjaj", false),
        ("esp-u002", "Signal izvor", false),
    ];

    public MockBluetoothScanService()
    {
    }

    public Task<bool> IsBluetoothEnabledAsync()
    {
        return Task.FromResult(_isBluetoothEnabled);
    }

    public async Task EnableBluetoothAsync()
    {
        await Task.Delay(500);
        _isBluetoothEnabled = true;
    }

    public async Task<ScanSession> RunMockScanAsync()
    {
        if (!_isBluetoothEnabled)
            await EnableBluetoothAsync();

        await Task.Delay(1200);

        var sessionId = Guid.NewGuid();
        var now = DateTime.Now;
        var random = new Random(sessionId.GetHashCode());

        // Generate a few route points (phone/drone path)
        var baseLat = 34.0522;
        var baseLng = -118.2437;
        var routePoints = new List<RoutePoint>();
        for (var i = 0; i < 3; i++)
        {
            routePoints.Add(new RoutePoint(
                baseLat + random.NextDouble() * 0.01,
                baseLng + random.NextDouble() * 0.01,
                now.AddSeconds(i * 30)));
        }

        // Generate broadcasts from every known device
        var broadcasts = new List<EspBroadcastReading>();
        foreach (var (uid, label, authenticated) in KnownDevices)
        {
            var deviceLat = baseLat + random.NextDouble() * 0.02 - 0.01;
            var deviceLng = baseLng + random.NextDouble() * 0.02 - 0.01;

            // Simulate reading every second or so
            var readingCount = random.Next(1, 3);
            for (var r = 0; r < readingCount; r++)
            {
                broadcasts.Add(new EspBroadcastReading(
                    Guid.NewGuid(),
                    sessionId,
                    uid,
                    label,
                    authenticated,
                    Math.Round(20.0 + random.NextDouble() * 15, 1),
                    Math.Round(30.0 + random.NextDouble() * 40, 1),
                    Math.Round(deviceLat, 6),
                    Math.Round(deviceLng, 6),
                    random.Next(-90, -40),
                    now.AddSeconds(r),
                    $"{{\"uid\":\"{uid}\",\"seq\":{r + 1}}}"));
            }
        }

        var session = new ScanSession(
            sessionId,
            $"Scan {now:HH:mm:ss}",
            now,
            now.AddSeconds(8),
            routePoints,
            broadcasts);

        _sessions.Add(session);
        return session;
    }

    public Task<IReadOnlyList<ScanSession>> GetScanSessionsAsync()
    {
        return Task.FromResult<IReadOnlyList<ScanSession>>(
            _sessions.OrderByDescending(s => s.StartedAt).ToList());
    }

    public Task<IReadOnlyList<EspBroadcastReading>> GetBroadcastsAsync(Guid sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        var broadcasts = session?.Broadcasts ?? [];
        return Task.FromResult<IReadOnlyList<EspBroadcastReading>>(broadcasts);
    }
}
