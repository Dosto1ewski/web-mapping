using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using NSubstitute;
using Standort.Application.DTOs;
using Standort.Application.Identity;
using Standort.Application.Interfaces;
using Standort.Application.Services;
using Standort.Domain.DomainExceptions;
using Standort.Domain.Entities;
using Standort.Domain.ValueObjects;
using Standort.UnitTests.TestHelpers;

namespace Standort.UnitTests.Application;

public class GroupServiceTests
{
    private readonly IGroupRepository _groupRepo = Substitute.For<IGroupRepository>();
    private readonly IInviteCodeRepository _inviteRepo = Substitute.For<IInviteCodeRepository>();
    private readonly ITokenGenerator _tokenGenerator = Substitute.For<ITokenGenerator>();
    private readonly ITokenHasher _tokenHasher = Substitute.For<ITokenHasher>();
    private readonly FakeClock _clock = new();

    private GroupService BuildService() => new(
        _groupRepo, _inviteRepo, _tokenGenerator, _tokenHasher, _clock);

    private static string HashCode(string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    [Fact]
    public async Task CreateGroup_StoresHashAndReturnsToken()
    {
        var hash = new string('a', 64); // arbitrary valid 64-char hex
        _tokenGenerator.GeneratePlainToken().Returns("plain-token-1");
        _tokenHasher.Hash("plain-token-1").Returns("hash-1");
        _inviteRepo.TryCreateAsync(hash, Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var svc = BuildService();
        var result = await svc.CreateGroupAsync(new CreateGroupRequest("My Group", "Antonin", hash), CancellationToken.None);

        result.MemberToken.Should().Be("plain-token-1");
        result.DisplayName.Should().Be("Antonin");
        result.GroupId.Should().NotBeNullOrEmpty();
        result.MemberId.Should().Be(MemberIdFactory.FromGroupAndNormalizedName(result.GroupId, "antonin"));

        await _inviteRepo.Received(1).TryCreateAsync(hash, result.GroupId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _groupRepo.Received(1).CreateGroupAsync(
            Arg.Is<Group>(g => g.GroupId == result.GroupId && g.Version == 0 && g.Name == "My Group"),
            Arg.Is<Member>(m => m.TokenHash == "hash-1" && m.DisplayName == "Antonin"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_UnknownInviteCode_Throws()
    {
        var hash = HashCode("XXXXXXXX-XXXXXXXX");
        _inviteRepo.GetGroupIdByCodeAsync(hash, Arg.Any<CancellationToken>()).Returns((string?)null);

        var svc = BuildService();
        var act = () => svc.JoinGroupAsync(new JoinGroupRequest("XXXXXXXX-XXXXXXXX", "Mira"), CancellationToken.None);

        await act.Should().ThrowAsync<InviteCodeNotFoundException>();
    }

    [Fact]
    public async Task JoinGroup_NewMember_BuildsFreshMember()
    {
        var inviteCode = "AAAABBBB-CCCCDDDD";
        var hash = HashCode(inviteCode);
        var groupId = Guid.NewGuid().ToString("D");
        _inviteRepo.GetGroupIdByCodeAsync(hash, Arg.Any<CancellationToken>()).Returns(groupId);
        _tokenGenerator.GeneratePlainToken().Returns("new-token");
        _tokenHasher.Hash("new-token").Returns("new-hash");

        Member? capturedNewMember = null;
        _groupRepo.ApplyMemberWriteAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Func<Group, Member?, Member>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var mutator = callInfo.Arg<Func<Group, Member?, Member>>();
                var fakeGroup = new Group { GroupId = groupId, Name = "n", CreatedAt = _clock.UtcNow, Version = 7 };
                capturedNewMember = mutator(fakeGroup, null);
                return Task.FromResult(capturedNewMember);
            });

        var svc = BuildService();
        await svc.JoinGroupAsync(new JoinGroupRequest(inviteCode, "Mira"), CancellationToken.None);

        capturedNewMember.Should().NotBeNull();
        capturedNewMember!.TokenHash.Should().Be("new-hash");
        capturedNewMember.CurrentLocation.Should().BeNull();
        capturedNewMember.RecentHistory.Should().BeEmpty();
        capturedNewMember.DisplayName.Should().Be("Mira");
    }

    [Fact]
    public async Task JoinGroup_Reclaim_RotatesTokenButPreservesLocation()
    {
        var inviteCode = "AAAABBBB-CCCCDDDD";
        var hash = HashCode(inviteCode);
        var groupId = Guid.NewGuid().ToString("D");
        var memberId = MemberIdFactory.FromGroupAndNormalizedName(groupId, "antonin");
        _inviteRepo.GetGroupIdByCodeAsync(hash, Arg.Any<CancellationToken>()).Returns(groupId);
        _tokenGenerator.GeneratePlainToken().Returns("rotated-token");
        _tokenHasher.Hash("rotated-token").Returns("rotated-hash");

        var existingLocation = new GeoCoordinate("e1.iv1.enc1", _clock.UtcNow.AddMinutes(-1), _clock.UtcNow.AddMinutes(-1));
        var existingHistory = new List<GeoCoordinate>
        {
            new("e1.iv2.enc2", _clock.UtcNow.AddMinutes(-3), _clock.UtcNow.AddMinutes(-3)),
        };
        var existing = new Member
        {
            MemberId = memberId,
            GroupId = groupId,
            DisplayName = "Antonin",
            DisplayNameNormalized = "antonin",
            TokenHash = "old-hash",
            TokenIssuedAt = _clock.UtcNow.AddDays(-1),
            CurrentLocation = existingLocation,
            RecentHistory = existingHistory,
            LastUpdatedVersion = 5,
        };

        Member? capturedNewMember = null;
        _groupRepo.ApplyMemberWriteAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Func<Group, Member?, Member>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var mutator = callInfo.Arg<Func<Group, Member?, Member>>();
                var fakeGroup = new Group { GroupId = groupId, Name = "n", CreatedAt = _clock.UtcNow, Version = 5 };
                capturedNewMember = mutator(fakeGroup, existing);
                return Task.FromResult(capturedNewMember);
            });

        var svc = BuildService();
        await svc.JoinGroupAsync(new JoinGroupRequest(inviteCode, "Antonin"), CancellationToken.None);

        capturedNewMember.Should().NotBeNull();
        capturedNewMember!.MemberId.Should().Be(memberId);
        capturedNewMember.TokenHash.Should().Be("rotated-hash");
        capturedNewMember.CurrentLocation.Should().Be(existingLocation);
        capturedNewMember.RecentHistory.Should().BeEquivalentTo(existingHistory);
        capturedNewMember.DisplayNameNormalized.Should().Be("antonin");
    }
}
