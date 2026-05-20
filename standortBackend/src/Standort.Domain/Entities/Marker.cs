namespace Standort.Domain.Entities;

public sealed record Marker
{
    public required string MarkerId { get; init; }
    public required string GroupId { get; init; }
    public required string Name { get; init; }
    public required string EncryptedLocation { get; init; }
    public string? EncryptedNotes { get; init; }
    public string? Color { get; init; }
    public string? Icon { get; init; }
    public required string CreatedByMemberId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
