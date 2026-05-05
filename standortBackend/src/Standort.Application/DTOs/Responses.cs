namespace Standort.Application.DTOs;

public sealed record CreateGroupResponse(
    string GroupId,
    string InviteCode,
    string MemberId,
    string MemberToken,
    string DisplayName);

public sealed record JoinGroupResponse(
    string GroupId,
    string MemberId,
    string MemberToken,
    string DisplayName);

public sealed record GeoPointDto(
    double Lat,
    double Lng,
    double AccuracyMeters,
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
