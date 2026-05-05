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
    private readonly IInviteCodeGenerator _inviteCodeGenerator = Substitute.For<IInviteCodeGenerator>();
    private readonly FakeClock _clock = new();

    private GroupService BuildService() => new(
        _groupRepo, _inviteRepo, _tokenGenerator, _tokenHasher, _inviteCodeGenerator, _clock);

    [Fact]
    public async Task CreateGroup_ReturnsTokenAndInviteCode()
    {
        _tokenGenerator.GeneratePlainToken().Returns("plain-token-1");
        _tokenHasher.Hash("plain-token-1").Returns("hash-1");
        _inviteCodeGenerator.GenerateInviteCode().Returns("CODE-1234");
        _inviteRepo.TryCreateAsync("CODE-1234", Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var svc = BuildService();
        var result = await svc.CreateGroupAsync(new CreateGroupRequest("My Group", "Antonin"), CancellationToken.None);

        result.InviteCode.Should().Be("CODE-1234");
        result.MemberToken.Should().Be("plain-token-1");
        result.DisplayName.Should().Be("Antonin");
        result.GroupId.Should().NotBeNullOrEmpty();
        result.MemberId.Should().Be(MemberIdFactory.FromGroupAndNormalizedName(result.GroupId, "antonin"));

        await _groupRepo.Received(1).CreateGroupAsync(
            Arg.Is<Group>(g => g.GroupId == result.GroupId && g.Version == 0 && g.Name == "My Group"),
            Arg.Is<Member>(m => m.TokenHash == "hash-1" && m.DisplayName == "Antonin"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroup_RetriesInviteCodeOnCollision()
    {
        _tokenGenerator.GeneratePlainToken().Returns("token");
        _tokenHasher.Hash(Arg.Any<string>()).Returns("h");
        _inviteCodeGenerator.GenerateInviteCode().Returns("CODE-AAAA", "CODE-BBBB");
        _inviteRepo.TryCreateAsync("CODE-AAAA", Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _inviteRepo.TryCreateAsync("CODE-BBBB", Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var svc = BuildService();
        var result = await svc.CreateGroupAsync(new CreateGroupRequest("g", "User"), CancellationToken.None);

        result.InviteCode.Should().Be("CODE-BBBB");
    }

    [Fact]
    public async Task JoinGroup_UnknownInviteCode_Throws()
    {
        _inviteRepo.GetGroupIdByCodeAsync("XXXX-XXXX", Arg.Any<CancellationToken>()).Returns((string?)null);

        var svc = BuildService();
        var act = () => svc.JoinGroupAsync(new JoinGroupRequest("XXXX-XXXX", "Mira"), CancellationToken.None);

        await act.Should().ThrowAsync<InviteCodeNotFoundException>();
    }

    [Fact]
    public async Task JoinGroup_NewMember_BuildsFreshMember()
    {
        var groupId = Guid.NewGuid().ToString("D");
        _inviteRepo.GetGroupIdByCodeAsync("CODE-1111", Arg.Any<CancellationToken>()).Returns(groupId);
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
        await svc.JoinGroupAsync(new JoinGroupRequest("CODE-1111", "Mira"), CancellationToken.None);

        capturedNewMember.Should().NotBeNull();
        capturedNewMember!.TokenHash.Should().Be("new-hash");
        capturedNewMember.CurrentLocation.Should().BeNull();
        capturedNewMember.RecentHistory.Should().BeEmpty();
        capturedNewMember.DisplayName.Should().Be("Mira");
    }

    [Fact]
    public async Task JoinGroup_Reclaim_RotatesTokenButPreservesLocation()
    {
        var groupId = Guid.NewGuid().ToString("D");
        var memberId = MemberIdFactory.FromGroupAndNormalizedName(groupId, "antonin");
        _inviteRepo.GetGroupIdByCodeAsync("CODE-1111", Arg.Any<CancellationToken>()).Returns(groupId);
        _tokenGenerator.GeneratePlainToken().Returns("rotated-token");
        _tokenHasher.Hash("rotated-token").Returns("rotated-hash");

        var existingLocation = new GeoCoordinate(49.0, 8.4, 10, _clock.UtcNow.AddMinutes(-1), _clock.UtcNow.AddMinutes(-1));
        var existingHistory = new List<GeoCoordinate>
        {
            new(48.9, 8.3, 10, _clock.UtcNow.AddMinutes(-3), _clock.UtcNow.AddMinutes(-3)),
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
        await svc.JoinGroupAsync(new JoinGroupRequest("CODE-1111", "Antonin"), CancellationToken.None);

        capturedNewMember.Should().NotBeNull();
        capturedNewMember!.MemberId.Should().Be(memberId);
        capturedNewMember.TokenHash.Should().Be("rotated-hash");
        capturedNewMember.CurrentLocation.Should().Be(existingLocation);
        capturedNewMember.RecentHistory.Should().BeEquivalentTo(existingHistory);
        capturedNewMember.DisplayNameNormalized.Should().Be("antonin");
    }
}
