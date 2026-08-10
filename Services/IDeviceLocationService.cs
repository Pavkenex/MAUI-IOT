using Microsoft.Maui.Devices.Sensors;

namespace MAUI_IOT.Services;

public interface IDeviceLocationService
{
    Task<Location?> GetCurrentLocationAsync(CancellationToken cancellationToken = default);
}
