namespace Standort.Infrastructure.CosmosDb.Documents;

public sealed class GeoPointDocument
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double AccuracyMeters { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset ServerReceivedAt { get; set; }
}
