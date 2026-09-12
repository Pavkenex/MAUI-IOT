using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public sealed record ApiResult<T>(bool Success, T? Value, string? Error, int StatusCode)
{
    public static ApiResult<T> Ok(T value, int statusCode) => new(true, value, null, statusCode);

    public static ApiResult<T> Fail(string error, int statusCode) => new(false, default, error, statusCode);
}

public interface IApiClient
{
    Task<ApiResult<AuthResponse>> RegisterAsync(string username, string password, CancellationToken cancellationToken = default);

    Task<ApiResult<AuthResponse>> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    Task<ApiResult<ReadingUploadResponse>> UploadReadingsAsync(
        IReadOnlyList<ReadingUploadItem> readings,
        string token,
        CancellationToken cancellationToken = default);
}
