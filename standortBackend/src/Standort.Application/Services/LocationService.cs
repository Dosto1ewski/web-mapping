using Standort.Application.DTOs;
using Standort.Application.Interfaces;
using Standort.Domain.DomainExceptions;
using Standort.Domain.Entities;
using Standort.Domain.ValueObjects;

namespace Standort.Application.Services;

public sealed class LocationService
{
    /// <summary>
    /// Hard upper bound on stored history entries, independent of the member's chosen
    /// duration. Protects the member document from exceeding Cosmos DB's 2 MB item limit
    /// when a member shares location at a high frequency over a long window.
    /// </summary>
    public const int HistoryHardCap = 500;

    public const int DefaultHistoryDurationMinutes = 15;
    public const int MaxHistoryDurationMinutes = 2880;

    private readonly IGroupRepository _groupRepo;
    private readonly ITokenHasher _tokenHasher;
    private readonly ISystemClock _clock;

    public LocationService(IGroupRepository groupRepo, ITokenHasher tokenHasher, ISystemClock clock)
    {
        _groupRepo = groupRepo;
        _tokenHasher = tokenHasher;
        _clock = clock;
    }

    public async Task UpdateLocationAsync(
        string groupId,
        string memberId,
        string presentedToken,
        UpdateLocationRequest request,
        CancellationToken ct)
    {
        var member = await _groupRepo.ReadMemberAsync(groupId, memberId, ct)
            ?? throw new MemberNotFoundException(groupId, memberId);

        if (!_tokenHasher.Verify(presentedToken, member.TokenHash))
        {
            throw new InvalidTokenException();
        }

        var now = _clock.UtcNow;
        var newPoint = new GeoCoordinate(
            EncryptedLocation: request.EncryptedLocation,
            RecordedAt: request.RecordedAt,
            ServerReceivedAt: now);

        await _groupRepo.ApplyMemberWriteAsync(
            groupId,
            memberId,
            (group, existing) =>
            {
                if (existing is null)
                {
                    // Should not happen — token verified above, member exists. Treat as concurrency.
                    throw new ConcurrencyException(
                        $"Member '{memberId}' disappeared between read and write.");
                }

                var newHistory = BuildShiftedHistory(
                    existing.CurrentLocation,
                    existing.RecentHistory,
                    existing.HistoryDurationMinutes,
                    now);

                return existing with
                {
                    CurrentLocation = newPoint,
                    RecentHistory = newHistory,
                };
            },
            ct);
    }

    /// <summary>
    /// Updates the member's history-duration preference. Does not prune existing history —
    /// pruning is applied on the member's next location update.
    /// </summary>
    public async Task UpdateMemberSettingsAsync(
        string groupId,
        string memberId,
        string presentedToken,
        UpdateMemberSettingsRequest request,
        CancellationToken ct)
    {
        var member = await _groupRepo.ReadMemberAsync(groupId, memberId, ct)
            ?? throw new MemberNotFoundException(groupId, memberId);

        if (!_tokenHasher.Verify(presentedToken, member.TokenHash))
        {
            throw new InvalidTokenException();
        }

        await _groupRepo.ApplyMemberWriteAsync(
            groupId,
            memberId,
            (group, existing) =>
            {
                if (existing is null)
                {
                    throw new ConcurrencyException(
                        $"Member '{memberId}' disappeared between read and write.");
                }
                return existing with { HistoryDurationMinutes = request.HistoryDurationMinutes };
            },
            ct);
    }

    /// <summary>
    /// Clears the member's stored history and current location. Auto-share is a client-only
    /// concept; the caller (frontend) is responsible for switching it off locally.
    /// </summary>
    public async Task DeleteHistoryAsync(
        string groupId,
        string memberId,
        string presentedToken,
        CancellationToken ct)
    {
        var member = await _groupRepo.ReadMemberAsync(groupId, memberId, ct)
            ?? throw new MemberNotFoundException(groupId, memberId);

        if (!_tokenHasher.Verify(presentedToken, member.TokenHash))
        {
            throw new InvalidTokenException();
        }

        await _groupRepo.ApplyMemberWriteAsync(
            groupId,
            memberId,
            (group, existing) =>
            {
                if (existing is null)
                {
                    throw new ConcurrencyException(
                        $"Member '{memberId}' disappeared between read and write.");
                }
                return existing with
                {
                    CurrentLocation = null,
                    RecentHistory = Array.Empty<GeoCoordinate>(),
                };
            },
            ct);
    }

    public async Task<GroupLocationsResponse?> GetLocationsAsync(
        string groupId,
        long? sinceVersion,
        CancellationToken ct)
    {
        var group = await _groupRepo.ReadGroupAsync(groupId, ct)
            ?? throw new GroupNotFoundException(groupId);

        if (sinceVersion is not null && sinceVersion.Value == group.Version)
        {
            return null; // Caller maps to 304.
        }

        var threshold = sinceVersion ?? -1;
        var members = new List<MemberLocationDto>();
        await foreach (var member in _groupRepo.QueryMembersSinceAsync(groupId, threshold, ct))
        {
            members.Add(MapMember(member));
        }

        return new GroupLocationsResponse(
            GroupId: groupId,
            Version: group.Version,
            Members: members);
    }

    // Convention: oldest entry first. The previously-current point becomes the newest
    // history entry. Entries older than the member's chosen duration window are dropped,
    // then the buffer is clamped to HistoryHardCap as a document-size safety net.
    private static IReadOnlyList<GeoCoordinate> BuildShiftedHistory(
        GeoCoordinate? previousCurrent,
        IReadOnlyList<GeoCoordinate> existingHistory,
        int historyDurationMinutes,
        DateTimeOffset now)
    {
        if (historyDurationMinutes <= 0)
        {
            return Array.Empty<GeoCoordinate>();
        }

        var cutoff = now - TimeSpan.FromMinutes(historyDurationMinutes);
        var combined = new List<GeoCoordinate>(existingHistory.Count + 1);
        combined.AddRange(existingHistory);
        if (previousCurrent is not null)
        {
            combined.Add(previousCurrent);
        }

        var kept = combined.Where(p => p.RecordedAt >= cutoff).ToList();
        if (kept.Count > HistoryHardCap)
        {
            kept = kept.GetRange(kept.Count - HistoryHardCap, HistoryHardCap);
        }
        return kept;
    }

    private static MemberLocationDto MapMember(Member member) => new(
        MemberId: member.MemberId,
        DisplayName: member.DisplayName,
        CurrentLocation: member.CurrentLocation is null
            ? null
            : new GeoPointDto(
                member.CurrentLocation.EncryptedLocation,
                member.CurrentLocation.RecordedAt),
        RecentHistory: member.RecentHistory
            .Select(p => new GeoPointDto(p.EncryptedLocation, p.RecordedAt))
            .ToList());
}
