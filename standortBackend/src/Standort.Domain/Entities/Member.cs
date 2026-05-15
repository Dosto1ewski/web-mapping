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

    /// <summary>
    /// How many minutes of past location pings this member wants kept as a trail.
    /// 0 = no history. Pruning is applied lazily on the member's next location update.
    /// </summary>
    public int HistoryDurationMinutes { get; init; } = 15;
}
