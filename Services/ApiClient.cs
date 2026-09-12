using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public sealed class ApiClient : IApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public ApiClient(HttpClient http) => _http = http;

    public Task<ApiResult<AuthResponse>> RegisterAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
        => PostAuthAsync("auth/register", username, password, cancellationToken);

    public Task<ApiResult<AuthResponse>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
        => PostAuthAsync("auth/login", username, password, cancellationToken);

    public async Task<ApiResult<ReadingUploadResponse>> UploadReadingsAsync(
        IReadOnlyList<ReadingUploadItem> readings,
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "readings")
            {
                Content = CreateJsonContent(new ReadingUploadRequest { Readings = readings.ToList() }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<ReadingUploadResponse>.Fail(
                    await ReadErrorAsync(response, cancellationToken),
                    (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<ReadingUploadResponse>(JsonOptions, cancellationToken);
            return payload is null
                ? ApiResult<ReadingUploadResponse>.Fail("Empty response from server.", (int)response.StatusCode)
                : ApiResult<ReadingUploadResponse>.Ok(payload, (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ApiResult<ReadingUploadResponse>.Fail("Network error. Check your connection.", 0);
        }
    }

    private async Task<ApiResult<AuthResponse>> PostAuthAsync(
        string route,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        try
        {
            using var content = CreateJsonContent(new { username, password });
            using var response = await _http.PostAsync(route, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<AuthResponse>.Fail(
                    await ReadErrorAsync(response, cancellationToken),
                    (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, cancellationToken);
            return payload is null
                ? ApiResult<AuthResponse>.Fail("Empty response from server.", (int)response.StatusCode)
                : ApiResult<AuthResponse>.Ok(payload, (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ApiResult<AuthResponse>.Fail("Network error. Check your connection.", 0);
        }
    }

    private static StringContent CreateJsonContent<T>(T value)
        => new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(error?.Error))
            {
                return error!.Error!;
            }
        }
        catch (JsonException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return $"Request failed ({(int)response.StatusCode}).";
    }
}
