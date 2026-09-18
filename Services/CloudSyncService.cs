using MAUI_IOT.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;

namespace MAUI_IOT.Services;

public sealed record CloudSyncResult(bool Success, bool Skipped, int Imported, string? Error)
{
    public static CloudSyncResult Ok(int imported) => new(true, false, imported, null);

    public static CloudSyncResult Skip() => new(false, true, 0, null);

    public static CloudSyncResult Failed(string error) => new(false, false, 0, error);
}

public sealed class CloudSyncService
{
    private const int PullLimit = 500;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(1);

    private readonly IEspReadingRepository _repository;
    private readonly IApiClient _api;
    private readonly IAuthService _auth;
    private readonly ILogger<CloudSyncService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private DateTimeOffset _lastSyncAtUtc = DateTimeOffset.MinValue;

    public CloudSyncService(
        IEspReadingRepository repository,
        IApiClient api,
        IAuthService auth,
        ILogger<CloudSyncService> logger)
    {
        _repository = repository;
        _api = api;
        _auth = auth;
        _logger = logger;
    }

    public event EventHandler? ReadingsUpdated;

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _auth.SessionChanged += OnSessionChanged;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

        var cts = new CancellationTokenSource();
        _cts = cts;
        _loop = Task.Run(() => RunAsync(cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        _auth.SessionChanged -= OnSessionChanged;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;

        await _cts.CancelAsync();
        try
        {
            if (_loop is not null)
            {
                await _loop;
            }
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    public void NotifyAppForegrounded() => _ = SyncAsync();

    public async Task<CloudSyncResult> SyncAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_auth.HasValidSession || _auth.Token is not { Length: > 0 } token)
            {
                return CloudSyncResult.Failed("Sign in to pull cloud readings.");
            }

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                return CloudSyncResult.Failed("No internet connection.");
            }

            if (!force && DateTimeOffset.UtcNow - _lastSyncAtUtc < MinInterval)
            {
                return CloudSyncResult.Skip();
            }

            var result = await _api.GetReadingsAsync(token, deviceId: null, limit: PullLimit, cancellationToken);
            if (!result.Success || result.Value is null)
            {
                if (result.StatusCode == 401)
                {
                    await _auth.LogoutAsync();
                }

                return CloudSyncResult.Failed(result.Error ?? "unknown error");
            }

            var imported = await _repository.ImportCloudReadingsAsync(
                result.Value.Readings.Select(MapCloudReading).ToList());

            _lastSyncAtUtc = DateTimeOffset.UtcNow;

            if (imported > 0)
            {
                ReadingsUpdated?.Invoke(this, EventArgs.Empty);
            }

            return CloudSyncResult.Ok(imported);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cloud pull failed.");
            return CloudSyncResult.Failed(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var delay = IdleDelay;

            try
            {
                var result = await SyncAsync(force: false, cancellationToken);
                if (!result.Success && !result.Skipped && _auth.HasValidSession)
                {
                    delay = ErrorDelay;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloud sync loop failed.");
                delay = ErrorDelay;
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (_auth.HasValidSession)
        {
            _ = SyncAsync(force: true);
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            _ = SyncAsync();
        }
    }

    private static EspReading MapCloudReading(ReadingListItem item) => new()
    {
        DeviceId = item.DeviceId,
        DeviceName = item.DeviceName,
        BootSessionId = item.BootSessionId,
        ReadingId = item.ReadingId,
        ElapsedSeconds = item.ElapsedSeconds,
        TemperatureCelsius = item.TemperatureCelsius,
        HumidityPercent = item.HumidityPercent,
        Latitude = item.Latitude,
        Longitude = item.Longitude,
        RecordedAtUtc = item.RecordedAtUtc,
        ReceivedAtUtc = item.ReceivedAtUtc,
    };
}
