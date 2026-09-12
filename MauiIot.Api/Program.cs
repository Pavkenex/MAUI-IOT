using MauiIot.Api.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton(_ => new CosmosClient(
            GetRequiredSetting("CosmosConnectionString"),
            new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                },
            }));

        services.AddSingleton(sp => new CosmosStore(
            sp.GetRequiredService<CosmosClient>(),
            GetRequiredSetting("CosmosDatabaseName")));

        services.AddSingleton<PasswordService>();
        services.AddSingleton(sp => new TokenService(GetRequiredSetting("JwtSigningKey")));
    })
    .Build();

host.Run();

static string GetRequiredSetting(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Missing required app setting '{name}'.");
    }

    return value;
}
