using Standort.Domain.ValueObjects;

namespace Standort.Domain.Entities;

public sealed record Member
{
    public required string MemberId { get; init; }
    public required string GroupId { get; init; }
    public required string DisplayName { get; init; }
    public required string DisplayNameNormalized { get; init; }
    public required string TokenHash { get; init; }
    public required DateTimeOffset TokenIssuedAt { get; init; }
    public GeoCoordinate? CurrentLocation { get; init; }
    public IReadOnlyList<GeoCoordinate> RecentHistory { get; init; } = Array.Empty<GeoCoordinate>();
    public long LastUpdatedVersion { get; init; }
}
