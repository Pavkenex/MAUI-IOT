using System.Net;
using System.Security.Claims;
using MauiIot.Api.Models;
using MauiIot.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace MauiIot.Api.Functions;

public sealed class ReadingsFunctions
{
    private const int MaxBatchSize = 500;

    private readonly CosmosStore _store;
    private readonly TokenService _tokens;

    public ReadingsFunctions(CosmosStore store, TokenService tokens)
    {
        _store = store;
        _tokens = tokens;
    }

    [Function("UploadReadings")]
    public async Task<HttpResponseData> UploadAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "readings")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var principal = Authenticate(request);
        var userId = principal?.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return await HttpHelpers.JsonAsync(
                request,
                HttpStatusCode.Unauthorized,
                new ErrorResponse { Error = "Missing or invalid token." },
                cancellationToken);
        }

        var body = await HttpHelpers.ReadBodyAsync<ReadingUploadRequest>(request);
        if (body?.Readings is null || body.Readings.Count == 0)
        {
            return await HttpHelpers.JsonAsync(
                request,
                HttpStatusCode.BadRequest,
                new ErrorResponse { Error = "No readings were supplied." },
                cancellationToken);
        }

        if (body.Readings.Count > MaxBatchSize)
        {
            return await HttpHelpers.JsonAsync(
                request,
                HttpStatusCode.BadRequest,
                new ErrorResponse { Error = $"A batch may contain at most {MaxBatchSize} readings." },
                cancellationToken);
        }

        var receivedAtUtc = DateTimeOffset.UtcNow;
        var documents = new List<ReadingDocument>(body.Readings.Count);

        foreach (var item in body.Readings)
        {
            if (string.IsNullOrWhiteSpace(item.DeviceId))
            {
                continue;
            }

            documents.Add(new ReadingDocument
            {
                Id = CosmosStore.BuildReadingId(userId, item.DeviceId, item.BootSessionId, item.ReadingId),
                UserId = userId,
                DeviceId = item.DeviceId,
                BootSessionId = item.BootSessionId,
                ReadingId = item.ReadingId,
                ElapsedSeconds = item.ElapsedSeconds,
                TemperatureCelsius = item.TemperatureCelsius,
                HumidityPercent = item.HumidityPercent,
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                RecordedAtUtc = item.RecordedAtUtc,
                ReceivedAtUtc = receivedAtUtc,
            });
        }

        var accepted = await _store.UpsertReadingsAsync(documents, cancellationToken);
        return await HttpHelpers.JsonAsync(
            request,
            HttpStatusCode.OK,
            new ReadingUploadResponse { Accepted = accepted },
            cancellationToken);
    }

    private ClaimsPrincipal? Authenticate(HttpRequestData request)
    {
        if (!request.Headers.TryGetValues("Authorization", out var values))
        {
            return null;
        }

        var header = values.FirstOrDefault();
        if (header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = header["Bearer ".Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : _tokens.ValidateToken(token);
    }
}
