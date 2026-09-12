using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace MauiIot.Api.Functions;

internal static class HttpHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<T?> ReadBodyAsync<T>(HttpRequestData request)
    {
        using var reader = new StreamReader(request.Body);
        var content = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static async Task<HttpResponseData> JsonAsync<T>(
        HttpRequestData request,
        HttpStatusCode statusCode,
        T payload,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);
        return response;
    }
}
