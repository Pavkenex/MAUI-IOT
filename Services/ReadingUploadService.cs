using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public sealed class ReadingUploadService
{
    private const int BatchSize = 200;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(60);

    private readonly IEspReadingRepository _repository;
    private readonly IApiClient _api;
    private readonly IAuthService _auth;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public ReadingUploadService(IEspReadingRepository repository, IApiClient api, IAuthService auth)
    {
        _repository = repository;
        _api = api;
        _auth = auth;
    }

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

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

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var delay = IdleDelay;

            try
            {
                if (_auth.HasValidSession && _auth.Token is { Length: > 0 } token)
                {
                    var pending = await _repository.GetPendingUploadsAsync(BatchSize);
                    if (pending.Count > 0)
                    {
                        var items = pending.Select(Map).ToList();
                        var result = await _api.UploadReadingsAsync(items, token, cancellationToken);

                        if (result.Success)
                        {
                            await _repository.MarkUploadedAsync(pending);
                        }
                        else if (result.StatusCode == 401)
                        {
                            await _auth.LogoutAsync();
                        }
                        else
                        {
                            delay = ErrorDelay;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
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

    private static ReadingUploadItem Map(EspReading reading) => new()
    {
        DeviceId = reading.DeviceId,
        BootSessionId = reading.BootSessionId,
        ReadingId = reading.ReadingId,
        ElapsedSeconds = reading.ElapsedSeconds,
        TemperatureCelsius = reading.TemperatureCelsius,
        HumidityPercent = reading.HumidityPercent,
        Latitude = reading.Latitude,
        Longitude = reading.Longitude,
        RecordedAtUtc = reading.RecordedAtUtc,
    };
}
