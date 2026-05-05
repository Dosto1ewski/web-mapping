using System.Text.Json;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Standort.Infrastructure.Configuration;

namespace Standort.Infrastructure.CosmosDb;

public static class CosmosClientFactory
{
    public static CosmosClient Create(CosmosDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.AccountEndpoint))
        {
            throw new InvalidOperationException("CosmosDb:AccountEndpoint is not configured.");
        }

        var clientOptions = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway, // safest for emulator + corporate networks
            UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            },
        };

        if (!string.IsNullOrWhiteSpace(options.AccountKey))
        {
            return new CosmosClient(options.AccountEndpoint, options.AccountKey, clientOptions);
        }

        return new CosmosClient(options.AccountEndpoint, new DefaultAzureCredential(), clientOptions);
    }
}
