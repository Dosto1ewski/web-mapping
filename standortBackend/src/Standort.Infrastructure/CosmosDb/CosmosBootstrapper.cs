using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Standort.Infrastructure.Configuration;

namespace Standort.Infrastructure.CosmosDb;

/// <summary>
/// Creates the Cosmos database and required containers on startup if they don't exist.
/// Intended for local emulator workflow; production deployments should provision via IaC.
/// </summary>
public sealed class CosmosBootstrapper
{
    private readonly CosmosClient _client;
    private readonly CosmosDbOptions _options;

    public CosmosBootstrapper(CosmosClient client, IOptions<CosmosDbOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task EnsureCreatedAsync(CancellationToken ct)
    {
        var dbResp = await _client.CreateDatabaseIfNotExistsAsync(_options.DatabaseName, cancellationToken: ct);
        var db = dbResp.Database;

        await db.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_options.GroupsContainerName, "/groupId")
            {
                DefaultTimeToLive = -1,
            },
            cancellationToken: ct);

        await db.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_options.InviteCodesContainerName, "/inviteCode")
            {
                DefaultTimeToLive = -1,
            },
            cancellationToken: ct);
    }
}
