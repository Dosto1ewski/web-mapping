using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Standort.Application.Interfaces;
using Standort.Domain.Entities;
using Standort.Infrastructure.Configuration;
using Standort.Infrastructure.CosmosDb.Documents;

namespace Standort.Infrastructure.CosmosDb;

public sealed class CosmosMarkerRepository : IMarkerRepository
{
    private readonly Container _container;

    public CosmosMarkerRepository(CosmosClient client, IOptions<CosmosDbOptions> options)
    {
        var opts = options.Value;
        _container = client.GetContainer(opts.DatabaseName, opts.GroupsContainerName);
    }

    public async Task<Marker> CreateMarkerAsync(Marker marker, CancellationToken ct)
    {
        var doc = marker.ToDocument();
        await _container.CreateItemAsync(doc, new PartitionKey(marker.GroupId), cancellationToken: ct);
        return marker;
    }

    public async IAsyncEnumerable<Marker> GetMarkersAsync(
        string groupId, [EnumeratorCancellation] CancellationToken ct)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = 'marker'");

        using var iter = _container.GetItemQueryIterator<MarkerDocument>(
            query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(groupId) });

        while (iter.HasMoreResults)
        {
            var page = await iter.ReadNextAsync(ct);
            foreach (var doc in page)
                yield return doc.ToDomain();
        }
    }

    public async Task<bool> DeleteMarkerAsync(string groupId, string markerId, CancellationToken ct)
    {
        try
        {
            await _container.DeleteItemAsync<MarkerDocument>(
                MarkerDocument.IdFor(markerId),
                new PartitionKey(groupId),
                cancellationToken: ct);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
