namespace Standort.Application.DTOs;

public sealed record CreateGroupRequest(string Name, string CreatedByDisplayName);

public sealed record JoinGroupRequest(string InviteCode, string DisplayName);

public sealed record UpdateLocationRequest(
    double Lat,
    double Lng,
    double AccuracyMeters,
    DateTimeOffset RecordedAt);

public sealed record CreateMarkerRequest(
    string Name,
    double Lat,
    double Lng,
    string? Color,
    string? Notes);
