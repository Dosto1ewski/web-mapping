using System.Security.Cryptography;
using System.Text;
using Standort.Application.DTOs;
using Standort.Application.Identity;
using Standort.Application.Interfaces;
using Standort.Domain.DomainExceptions;
using Standort.Domain.Entities;

namespace Standort.Application.Services;

public sealed class GroupService
{
    private readonly IGroupRepository _groupRepo;
    private readonly IInviteCodeRepository _inviteRepo;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly ITokenHasher _tokenHasher;
    private readonly ISystemClock _clock;

    public GroupService(
        IGroupRepository groupRepo,
        IInviteCodeRepository inviteRepo,
        ITokenGenerator tokenGenerator,
        ITokenHasher tokenHasher,
        ISystemClock clock)
    {
        _groupRepo = groupRepo;
        _inviteRepo = inviteRepo;
        _tokenGenerator = tokenGenerator;
        _tokenHasher = tokenHasher;
        _clock = clock;
    }

    public async Task<CreateGroupResponse> CreateGroupAsync(CreateGroupRequest request, CancellationToken ct)
    {
        var groupId = Guid.NewGuid().ToString("D");
        var now = _clock.UtcNow;
        var displayName = request.CreatedByDisplayName.Trim();
        var normalized = DisplayNameNormalizer.Normalize(displayName);
        var memberId = MemberIdFactory.FromGroupAndNormalizedName(groupId, normalized);
        var plainToken = _tokenGenerator.GeneratePlainToken();
        var tokenHash = _tokenHasher.Hash(plainToken);

        if (!await _inviteRepo.TryCreateAsync(request.InviteCodeHash, groupId, now, ct))
            throw new InvalidOperationException("Invite code hash already in use.");

        var group = new Group
        {
            GroupId = groupId,
            Name = request.Name.Trim(),
            CreatedAt = now,
            Version = 0,
        };

        var member = new Member
        {
            MemberId = memberId,
            GroupId = groupId,
            DisplayName = displayName,
            DisplayNameNormalized = normalized,
            TokenHash = tokenHash,
            TokenIssuedAt = now,
            CurrentLocation = null,
            RecentHistory = Array.Empty<Domain.ValueObjects.GeoCoordinate>(),
            LastUpdatedVersion = 0,
        };

        await _groupRepo.CreateGroupAsync(group, member, ct);

        return new CreateGroupResponse(
            GroupId: groupId,
            MemberId: memberId,
            MemberToken: plainToken,
            DisplayName: displayName,
            HistoryDurationMinutes: member.HistoryDurationMinutes);
    }

    // A member is considered "active" if either their token was issued or their
    // last location was received within this window. A join attempt with the
    // same name during this window must be confirmed by the user (takeover).
    private static readonly TimeSpan ActiveMemberWindow = TimeSpan.FromMinutes(2);

    public async Task<JoinGroupResponse> JoinGroupAsync(JoinGroupRequest request, CancellationToken ct)
    {
        var hash = HashInviteCode(request.InviteCode);
        var groupId = await _inviteRepo.GetGroupIdByCodeAsync(hash, ct)
            ?? throw new InviteCodeNotFoundException(request.InviteCode);

        var displayName = request.DisplayName.Trim();
        var normalized = DisplayNameNormalizer.Normalize(displayName);
        var memberId = MemberIdFactory.FromGroupAndNormalizedName(groupId, normalized);
        var plainToken = _tokenGenerator.GeneratePlainToken();
        var tokenHash = _tokenHasher.Hash(plainToken);
        var now = _clock.UtcNow;

        var written = await _groupRepo.ApplyMemberWriteAsync(
            groupId,
            memberId,
            (group, existing) =>
            {
                if (existing is null)
                {
                    return new Member
                    {
                        MemberId = memberId,
                        GroupId = groupId,
                        DisplayName = displayName,
                        DisplayNameNormalized = normalized,
                        TokenHash = tokenHash,
                        TokenIssuedAt = now,
                        CurrentLocation = null,
                        RecentHistory = Array.Empty<Domain.ValueObjects.GeoCoordinate>(),
                        // LastUpdatedVersion is overridden by the repository.
                        LastUpdatedVersion = 0,
                    };
                }

                if (!request.Takeover)
                {
                    var lastSeen = existing.CurrentLocation is { } loc && loc.ServerReceivedAt > existing.TokenIssuedAt
                        ? loc.ServerReceivedAt
                        : existing.TokenIssuedAt;
                    if (now - lastSeen < ActiveMemberWindow)
                    {
                        throw new NameInUseException(existing.DisplayName, lastSeen, existing.CurrentLocation is not null);
                    }
                }

                // Reclaim: rotate token, keep displayName casing as latest,
                // preserve location/history and the member's settings.
                return existing with
                {
                    DisplayName = displayName,
                    TokenHash = tokenHash,
                    TokenIssuedAt = now,
                };
            },
            ct);

        return new JoinGroupResponse(
            GroupId: groupId,
            MemberId: memberId,
            MemberToken: plainToken,
            DisplayName: displayName,
            HistoryDurationMinutes: written.HistoryDurationMinutes);
    }

    private static string HashInviteCode(string inviteCode)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(inviteCode));
        return Convert.ToHexStringLower(bytes);
    }
}
