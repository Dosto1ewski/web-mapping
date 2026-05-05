namespace Standort.Domain.ValueObjects;

public sealed record GeoCoordinate(
    double Lat,
    double Lng,
    double AccuracyMeters,
    DateTimeOffset RecordedAt,
    DateTimeOffset ServerReceivedAt);
