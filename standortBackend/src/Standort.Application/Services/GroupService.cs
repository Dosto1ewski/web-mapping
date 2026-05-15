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
    private readonly IInviteCodeGenerator _inviteCodeGenerator;
    private readonly ISystemClock _clock;

    private const int InviteCodeMaxAttempts = 5;

    public GroupService(
        IGroupRepository groupRepo,
        IInviteCodeRepository inviteRepo,
        ITokenGenerator tokenGenerator,
        ITokenHasher tokenHasher,
        IInviteCodeGenerator inviteCodeGenerator,
        ISystemClock clock)
    {
        _groupRepo = groupRepo;
        _inviteRepo = inviteRepo;
        _tokenGenerator = tokenGenerator;
        _tokenHasher = tokenHasher;
        _inviteCodeGenerator = inviteCodeGenerator;
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

        var inviteCode = await ClaimInviteCodeAsync(groupId, now, ct);

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
            InviteCode: inviteCode,
            MemberId: memberId,
            MemberToken: plainToken,
            DisplayName: displayName,
            HistoryDurationMinutes: member.HistoryDurationMinutes);
    }

    public async Task<JoinGroupResponse> JoinGroupAsync(JoinGroupRequest request, CancellationToken ct)
    {
        var groupId = await _inviteRepo.GetGroupIdByCodeAsync(request.InviteCode, ct)
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

    private async Task<string> ClaimInviteCodeAsync(string groupId, DateTimeOffset now, CancellationToken ct)
    {
        for (var attempt = 0; attempt < InviteCodeMaxAttempts; attempt++)
        {
            var code = _inviteCodeGenerator.GenerateInviteCode();
            if (await _inviteRepo.TryCreateAsync(code, groupId, now, ct))
            {
                return code;
            }
        }
        throw new InvalidOperationException(
            $"Could not generate a unique invite code after {InviteCodeMaxAttempts} attempts.");
    }
}
