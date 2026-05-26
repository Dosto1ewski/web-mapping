namespace Standort.Application.DTOs;

public sealed record CreateGroupRequest(string Name, string CreatedByDisplayName, string InviteCodeHash);

public sealed record JoinGroupRequest(string InviteCode, string DisplayName, bool Takeover = false);

public sealed record UpdateLocationRequest(string EncryptedLocation, DateTimeOffset RecordedAt);

public sealed record UpdateMemberSettingsRequest(int HistoryDurationMinutes);

public sealed record CreateMarkerRequest(
    string Name,
    string EncryptedLocation,
    string? EncryptedNotes,
    string? Color,
    string? Icon);
