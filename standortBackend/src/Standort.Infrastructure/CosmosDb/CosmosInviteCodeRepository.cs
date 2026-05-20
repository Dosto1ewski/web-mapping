using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Standort.Application.Interfaces;
using Standort.Infrastructure.Configuration;
using Standort.Infrastructure.CosmosDb.Documents;

namespace Standort.Infrastructure.CosmosDb;

public sealed class CosmosInviteCodeRepository : IInviteCodeRepository
{
    private readonly Container _container;

    public CosmosInviteCodeRepository(CosmosClient client, IOptions<CosmosDbOptions> options)
    {
        var opts = options.Value;
        _container = client.GetContainer(opts.DatabaseName, opts.InviteCodesContainerName);
    }

    public async Task<bool> TryCreateAsync(
        string inviteCode, string groupId, DateTimeOffset createdAt, CancellationToken ct)
    {
        var doc = new InviteCodeDocument
        {
            Id = inviteCode,
            InviteCodeHash = inviteCode,
            GroupId = groupId,
            CreatedAt = createdAt,
        };
        try
        {
            await _container.CreateItemAsync(doc, new PartitionKey(inviteCode), cancellationToken: ct);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }

    public async Task<string?> GetGroupIdByCodeAsync(string inviteCode, CancellationToken ct)
    {
        try
        {
            var resp = await _container.ReadItemAsync<InviteCodeDocument>(
                inviteCode, new PartitionKey(inviteCode), cancellationToken: ct);
            return resp.Resource.GroupId;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
