namespace Standort.Infrastructure.CosmosDb.Documents;

public sealed class GeoPointDocument
{
    public string EncryptedLocation { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset ServerReceivedAt { get; set; }
}
