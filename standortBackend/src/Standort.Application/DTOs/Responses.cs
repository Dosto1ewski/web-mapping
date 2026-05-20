namespace Standort.Application.DTOs;

public sealed record CreateGroupResponse(
    string GroupId,
    string MemberId,
    string MemberToken,
    string DisplayName,
    int HistoryDurationMinutes);

public sealed record JoinGroupResponse(
    string GroupId,
    string MemberId,
    string MemberToken,
    string DisplayName,
    int HistoryDurationMinutes);

public sealed record GeoPointDto(
    string EncryptedLocation,
    DateTimeOffset RecordedAt);

public sealed record MemberLocationDto(
    string MemberId,
    string DisplayName,
    GeoPointDto? CurrentLocation,
    IReadOnlyList<GeoPointDto> RecentHistory);

public sealed record GroupLocationsResponse(
    string GroupId,
    long Version,
    IReadOnlyList<MemberLocationDto> Members);

public sealed record MarkerDto(
    string MarkerId,
    string Name,
    string EncryptedLocation,
    string? EncryptedNotes,
    string? Color,
    string? Icon,
    string CreatedByMemberId,
    DateTimeOffset CreatedAt);
