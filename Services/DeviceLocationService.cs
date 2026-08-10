using Microsoft.Maui.Devices.Sensors;

namespace MAUI_IOT.Services;

public sealed class DeviceLocationService : IDeviceLocationService
{
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FixTimeout = TimeSpan.FromSeconds(10);

    private Location? _cachedLocation;
    private DateTimeOffset _cachedAtUtc;

    public async Task<Location?> GetCurrentLocationAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedLocation is not null && DateTimeOffset.UtcNow - _cachedAtUtc < CacheMaxAge)
        {
            return _cachedLocation;
        }

        if (!await TryEnsurePermissionAsync())
        {
            return null;
        }

        var location = await Geolocation.Default.GetLastKnownLocationAsync()
            ?? await TryGetFreshFixAsync(cancellationToken);

        if (location is not null)
        {
            _cachedLocation = location;
            _cachedAtUtc = DateTimeOffset.UtcNow;
        }

        return location;
    }

    private static async Task<bool> TryEnsurePermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            return status == PermissionStatus.Granted;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<Location?> TryGetFreshFixAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(FixTimeout);

            return await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)),
                cts.Token);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
