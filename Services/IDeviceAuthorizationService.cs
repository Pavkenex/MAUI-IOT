namespace MAUI_IOT.Services;

public interface IDeviceAuthorizationService
{
    Task<bool> IsAuthorizedAsync(string espDeviceId);
}
