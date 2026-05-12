namespace Standort.Domain.Entities;

public sealed record Marker
{
    public required string MarkerId { get; init; }
    public required string GroupId { get; init; }
    public required string Name { get; init; }
    public required double Lat { get; init; }
    public required double Lng { get; init; }
    public string? Color { get; init; }
    public string? Notes { get; init; }
    public required string CreatedByMemberId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
