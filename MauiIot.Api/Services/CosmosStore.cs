using System.Net;
using MauiIot.Api.Models;
using Microsoft.Azure.Cosmos;

namespace MauiIot.Api.Services;

public sealed class CosmosStore
{
    public const string UsersContainerName = "users";
    public const string ReadingsContainerName = "readings";

    private readonly Container _users;
    private readonly Container _readings;
    private readonly Lazy<Task> _initialization;

    public CosmosStore(CosmosClient client, string databaseName)
    {
        _users = client.GetContainer(databaseName, UsersContainerName);
        _readings = client.GetContainer(databaseName, ReadingsContainerName);
        _initialization = new Lazy<Task>(() => EnsureCreatedAsync(client, databaseName));
    }

    public async Task<UserDocument?> GetUserAsync(string id, CancellationToken cancellationToken = default)
    {
        await _initialization.Value;

        try
        {
            var response = await _users.ReadItemAsync<UserDocument>(id, new PartitionKey(id), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> CreateUserAsync(UserDocument user, CancellationToken cancellationToken = default)
    {
        await _initialization.Value;

        try
        {
            await _users.CreateItemAsync(user, new PartitionKey(user.Id), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }

    public async Task<int> UpsertReadingsAsync(IReadOnlyList<ReadingDocument> readings, CancellationToken cancellationToken = default)
    {
        await _initialization.Value;

        var accepted = 0;
        foreach (var reading in readings)
        {
            await _readings.UpsertItemAsync(reading, new PartitionKey(reading.UserId), cancellationToken: cancellationToken);
            accepted++;
        }

        return accepted;
    }

    public static string BuildReadingId(string userId, string deviceId, uint bootSessionId, uint readingId)
        => $"{userId}:{deviceId}:{bootSessionId}:{readingId}";

    private static async Task EnsureCreatedAsync(CosmosClient client, string databaseName)
    {
        await client.CreateDatabaseIfNotExistsAsync(databaseName);
        var database = client.GetDatabase(databaseName);
        await database.CreateContainerIfNotExistsAsync(UsersContainerName, "/id");
        await database.CreateContainerIfNotExistsAsync(ReadingsContainerName, "/userId");
    }
}
