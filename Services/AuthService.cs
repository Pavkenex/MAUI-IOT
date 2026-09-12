using System.Globalization;
using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public sealed class AuthService : IAuthService
{
    private const string TokenKey = "auth_token";
    private const string UsernameKey = "auth_username";
    private const string ExpiresKey = "auth_expires_utc";

    private readonly IApiClient _api;

    private string? _token;
    private string? _username;
    private DateTimeOffset _expiresAtUtc;

    public AuthService(IApiClient api) => _api = api;

    public string? Username => _username;

    public string? Token => _token;

    public bool HasValidSession => !string.IsNullOrEmpty(_token) && _expiresAtUtc > DateTimeOffset.UtcNow;

    public event EventHandler? SessionChanged;

    public async Task InitializeAsync()
    {
        _token = await SessionStore.GetAsync(TokenKey);
        _username = await SessionStore.GetAsync(UsernameKey);

        var expiresRaw = await SessionStore.GetAsync(ExpiresKey);
        _expiresAtUtc = DateTimeOffset.TryParse(
            expiresRaw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : default;

        if (!HasValidSession)
        {
            ClearInMemory();
            SessionStore.Remove(TokenKey);
            SessionStore.Remove(UsernameKey);
            SessionStore.Remove(ExpiresKey);
        }
    }

    public async Task<ApiResult<AuthResponse>> RegisterAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var result = await _api.RegisterAsync(username, password, cancellationToken);
        await ApplySessionAsync(result);
        return result;
    }

    public async Task<ApiResult<AuthResponse>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var result = await _api.LoginAsync(username, password, cancellationToken);
        await ApplySessionAsync(result);
        return result;
    }

    public Task LogoutAsync()
    {
        ClearInMemory();
        SessionStore.Remove(TokenKey);
        SessionStore.Remove(UsernameKey);
        SessionStore.Remove(ExpiresKey);
        SessionChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private async Task ApplySessionAsync(ApiResult<AuthResponse> result)
    {
        if (!result.Success || result.Value is null)
        {
            return;
        }

        _token = result.Value.Token;
        _username = result.Value.Username;
        _expiresAtUtc = result.Value.ExpiresAtUtc;

        await SessionStore.SetAsync(TokenKey, _token);
        await SessionStore.SetAsync(UsernameKey, _username);
        await SessionStore.SetAsync(ExpiresKey, _expiresAtUtc.ToString("O", CultureInfo.InvariantCulture));

        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearInMemory()
    {
        _token = null;
        _username = null;
        _expiresAtUtc = default;
    }
}
