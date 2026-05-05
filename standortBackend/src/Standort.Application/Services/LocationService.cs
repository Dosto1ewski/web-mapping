using Standort.Application.DTOs;
using Standort.Application.Interfaces;
using Standort.Domain.DomainExceptions;
using Standort.Domain.Entities;
using Standort.Domain.ValueObjects;

namespace Standort.Application.Services;

public sealed class LocationService
{
    public const int RecentHistoryMax = 5;

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
            Lat: request.Lat,
            Lng: request.Lng,
            AccuracyMeters: request.AccuracyMeters,
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

                var newHistory = BuildShiftedHistory(existing.CurrentLocation, existing.RecentHistory);

                return new Member
                {
                    MemberId = existing.MemberId,
                    GroupId = existing.GroupId,
                    DisplayName = existing.DisplayName,
                    DisplayNameNormalized = existing.DisplayNameNormalized,
                    TokenHash = existing.TokenHash,
                    TokenIssuedAt = existing.TokenIssuedAt,
                    CurrentLocation = newPoint,
                    RecentHistory = newHistory,
                    LastUpdatedVersion = existing.LastUpdatedVersion,
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
    // history entry; the oldest entries are dropped when the buffer exceeds RecentHistoryMax.
    private static IReadOnlyList<GeoCoordinate> BuildShiftedHistory(
        GeoCoordinate? previousCurrent,
        IReadOnlyList<GeoCoordinate> existingHistory)
    {
        if (previousCurrent is null)
        {
            return existingHistory;
        }
        var combined = new List<GeoCoordinate>(existingHistory.Count + 1);
        combined.AddRange(existingHistory);
        combined.Add(previousCurrent);
        if (combined.Count <= RecentHistoryMax)
        {
            return combined;
        }
        return combined.GetRange(combined.Count - RecentHistoryMax, RecentHistoryMax);
    }

    private static MemberLocationDto MapMember(Member member) => new(
        MemberId: member.MemberId,
        DisplayName: member.DisplayName,
        CurrentLocation: member.CurrentLocation is null
            ? null
            : new GeoPointDto(
                member.CurrentLocation.Lat,
                member.CurrentLocation.Lng,
                member.CurrentLocation.AccuracyMeters,
                member.CurrentLocation.RecordedAt),
        RecentHistory: member.RecentHistory
            .Select(p => new GeoPointDto(p.Lat, p.Lng, p.AccuracyMeters, p.RecordedAt))
            .ToList());
}
