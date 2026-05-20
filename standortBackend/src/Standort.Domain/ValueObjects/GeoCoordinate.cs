namespace Standort.Domain.ValueObjects;

public sealed record GeoCoordinate(
    string EncryptedLocation,
    DateTimeOffset RecordedAt,
    DateTimeOffset ServerReceivedAt);
