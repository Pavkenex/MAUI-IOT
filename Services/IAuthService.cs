using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public interface IAuthService
{
    string? Username { get; }

    string? Token { get; }

    bool HasValidSession { get; }

    event EventHandler? SessionChanged;

    Task InitializeAsync();

    Task<ApiResult<AuthResponse>> RegisterAsync(string username, string password, CancellationToken cancellationToken = default);

    Task<ApiResult<AuthResponse>> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    Task LogoutAsync();
}
