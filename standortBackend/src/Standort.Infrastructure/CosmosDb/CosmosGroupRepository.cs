using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Standort.Application.Interfaces;
using Standort.Domain.DomainExceptions;
using Standort.Domain.Entities;
using Standort.Infrastructure.Configuration;
using Standort.Infrastructure.CosmosDb.Documents;

namespace Standort.Infrastructure.CosmosDb;

public sealed class CosmosGroupRepository : IGroupRepository
{
    private const int MaxConcurrencyRetries = 3;

    private readonly Container _container;

    public CosmosGroupRepository(CosmosClient client, IOptions<CosmosDbOptions> options)
    {
        var opts = options.Value;
        _container = client.GetContainer(opts.DatabaseName, opts.GroupsContainerName);
    }

    public async Task<Group?> ReadGroupAsync(string groupId, CancellationToken ct)
    {
        try
        {
            var resp = await _container.ReadItemAsync<GroupDocument>(
                GroupDocument.GroupDocId, new PartitionKey(groupId), cancellationToken: ct);
            return resp.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Member?> ReadMemberAsync(string groupId, string memberId, CancellationToken ct)
    {
        try
        {
            var resp = await _container.ReadItemAsync<MemberDocument>(
                MemberDocument.IdFor(memberId), new PartitionKey(groupId), cancellationToken: ct);
            return resp.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<Member> QueryMembersSinceAsync(
        string groupId, long sinceVersion, [EnumeratorCancellation] CancellationToken ct)
    {
        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'member' AND c.lastUpdatedVersion > @v")
            .WithParameter("@v", sinceVersion);

        using var iter = _container.GetItemQueryIterator<MemberDocument>(
            query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(groupId) });

        while (iter.HasMoreResults)
        {
            var page = await iter.ReadNextAsync(ct);
            foreach (var doc in page)
            {
                yield return doc.ToDomain();
            }
        }
    }

    public async Task CreateGroupAsync(Group group, Member firstMember, CancellationToken ct)
    {
        var pk = new PartitionKey(group.GroupId);
        var batch = _container.CreateTransactionalBatch(pk);
        batch.CreateItem(group.ToDocument());

        var memberDoc = firstMember.ToDocument();
        memberDoc.LastUpdatedVersion = 0;
        batch.CreateItem(memberDoc);

        using var response = await batch.ExecuteAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ConcurrencyException(
                $"CreateGroup batch failed for group '{group.GroupId}': {response.StatusCode}");
        }
    }

    public async Task<Member> ApplyMemberWriteAsync(
        string groupId,
        string memberId,
        Func<Group, Member?, Member> mutator,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var (success, member) = await TryApplyMemberWriteAsync(groupId, memberId, mutator, ct);
            if (success)
            {
                return member!;
            }
        }
        throw new ConcurrencyException(
            $"ApplyMemberWriteAsync for member '{memberId}' in group '{groupId}' failed after {MaxConcurrencyRetries + 1} attempts.");
    }

    private async Task<(bool Success, Member? Member)> TryApplyMemberWriteAsync(
        string groupId,
        string memberId,
        Func<Group, Member?, Member> mutator,
        CancellationToken ct)
    {
        var pk = new PartitionKey(groupId);

        ItemResponse<GroupDocument> groupResp;
        try
        {
            groupResp = await _container.ReadItemAsync<GroupDocument>(
                GroupDocument.GroupDocId, pk, cancellationToken: ct);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new GroupNotFoundException(groupId);
        }
        var group = groupResp.Resource.ToDomain();
        var groupEtag = groupResp.ETag;

        Member? existingMember = null;
        string? memberEtag = null;
        var memberDocId = MemberDocument.IdFor(memberId);
        try
        {
            var memberResp = await _container.ReadItemAsync<MemberDocument>(
                memberDocId, pk, cancellationToken: ct);
            existingMember = memberResp.Resource.ToDomain();
            memberEtag = memberResp.ETag;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Will be a Create in the batch.
        }

        var mutated = mutator(group, existingMember);
        var newVersion = group.Version + 1;
        var resultingMember = mutated with { LastUpdatedVersion = newVersion };
        var memberDoc = resultingMember.ToDocument();

        var batch = _container.CreateTransactionalBatch(pk);
        batch.PatchItem(
            GroupDocument.GroupDocId,
            new[] { PatchOperation.Increment("/version", 1L) },
            new TransactionalBatchPatchItemRequestOptions { IfMatchEtag = groupEtag });

        if (existingMember is null)
        {
            batch.CreateItem(memberDoc);
        }
        else
        {
            batch.ReplaceItem(
                memberDocId,
                memberDoc,
                new TransactionalBatchItemRequestOptions { IfMatchEtag = memberEtag });
        }

        using var batchResponse = await batch.ExecuteAsync(ct);
        if (batchResponse.IsSuccessStatusCode)
        {
            return (true, resultingMember);
        }

        // Retry-able statuses: PreconditionFailed (412) means another writer beat us; Conflict (409) on Create
        // means a concurrent join created the same member doc.
        if (batchResponse.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
        {
            return (false, null);
        }

        throw new ConcurrencyException(
            $"ApplyMemberWriteAsync batch returned {batchResponse.StatusCode} for member '{memberId}'.");
    }
}
