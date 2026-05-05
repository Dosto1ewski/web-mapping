namespace Standort.Domain.Entities;

public sealed record Group
{
    public required string GroupId { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public long Version { get; init; }
}
