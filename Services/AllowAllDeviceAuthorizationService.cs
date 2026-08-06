namespace MAUI_IOT.Services;

public sealed class AllowAllDeviceAuthorizationService : IDeviceAuthorizationService
{
    public Task<bool> IsAuthorizedAsync(string espDeviceId)
    {
        return Task.FromResult(true);
    }
}
