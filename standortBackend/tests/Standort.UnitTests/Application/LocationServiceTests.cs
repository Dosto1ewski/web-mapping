using FluentAssertions;
using NSubstitute;
using Standort.Application.DTOs;
using Standort.Application.Interfaces;
using Standort.Application.Services;
using Standort.Domain.DomainExceptions;
using Standort.Domain.Entities;
using Standort.Domain.ValueObjects;
using Standort.UnitTests.TestHelpers;

namespace Standort.UnitTests.Application;

public class LocationServiceTests
{
    private readonly IGroupRepository _groupRepo = Substitute.For<IGroupRepository>();
    private readonly ITokenHasher _tokenHasher = Substitute.For<ITokenHasher>();
    private readonly FakeClock _clock = new();

    private LocationService BuildService() => new(_groupRepo, _tokenHasher, _clock);

    private Member BuildMember(
        GeoCoordinate? current = null,
        IReadOnlyList<GeoCoordinate>? history = null,
        int historyDurationMinutes = 15) => new()
    {
        MemberId = "m1",
        GroupId = "g1",
        DisplayName = "Antonin",
        DisplayNameNormalized = "antonin",
        TokenHash = "hash",
        TokenIssuedAt = _clock.UtcNow,
        CurrentLocation = current,
        RecentHistory = history ?? Array.Empty<GeoCoordinate>(),
        LastUpdatedVersion = 0,
        HistoryDurationMinutes = historyDurationMinutes,
    };

    // Wires _groupRepo.ApplyMemberWriteAsync to run the mutator against `existing` and capture the result.
    private void CaptureMemberWrite(Member existing, Action<Member> onCaptured, long groupVersion = 0)
    {
        _groupRepo.ApplyMemberWriteAsync("g1", "m1",
                Arg.Any<Func<Group, Member?, Member>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var mutator = ci.Arg<Func<Group, Member?, Member>>();
                var grp = new Group { GroupId = "g1", Name = "n", CreatedAt = _clock.UtcNow, Version = groupVersion };
                var result = mutator(grp, existing);
                onCaptured(result);
                return Task.FromResult(result);
            });
    }

    [Fact]
    public async Task UpdateLocation_RejectsBadToken()
    {
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns(BuildMember());
        _tokenHasher.Verify("bad", "hash").Returns(false);

        var svc = BuildService();
        var act = () => svc.UpdateLocationAsync("g1", "m1", "bad",
            new UpdateLocationRequest("e1.iv.enc", _clock.UtcNow), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidTokenException>();
    }

    [Fact]
    public async Task UpdateLocation_MemberMissing_Throws()
    {
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns((Member?)null);

        var svc = BuildService();
        var act = () => svc.UpdateLocationAsync("g1", "m1", "any",
            new UpdateLocationRequest("e1.iv.enc", _clock.UtcNow), CancellationToken.None);

        await act.Should().ThrowAsync<MemberNotFoundException>();
    }

    [Fact]
    public async Task UpdateLocation_PreviousCurrent_BecomesNewestHistoryEntry()
    {
        var prevCurrent = new GeoCoordinate("e1.iv.prev", _clock.UtcNow.AddMinutes(-1), _clock.UtcNow.AddMinutes(-1));
        var existing = BuildMember(current: prevCurrent, history: Array.Empty<GeoCoordinate>());

        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns(existing);
        _tokenHasher.Verify("ok", "hash").Returns(true);

        Member? captured = null;
        _groupRepo.ApplyMemberWriteAsync("g1", "m1",
                Arg.Any<Func<Group, Member?, Member>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var mutator = ci.Arg<Func<Group, Member?, Member>>();
                var grp = new Group { GroupId = "g1", Name = "n", CreatedAt = _clock.UtcNow, Version = 0 };
                captured = mutator(grp, existing);
                return Task.FromResult(captured);
            });

        var svc = BuildService();
        await svc.UpdateLocationAsync("g1", "m1", "ok",
            new UpdateLocationRequest("e1.iv.new", _clock.UtcNow), CancellationToken.None);

        captured!.CurrentLocation!.EncryptedLocation.Should().Be("e1.iv.new");
        captured.RecentHistory.Should().HaveCount(1);
        captured.RecentHistory[0].Should().Be(prevCurrent);
    }

    [Fact]
    public async Task UpdateLocation_DropsHistoryEntriesOlderThanDurationWindow()
    {
        var stale = new GeoCoordinate("e1.iv.stale", _clock.UtcNow.AddMinutes(-30), _clock.UtcNow.AddMinutes(-30));
        var fresh = new GeoCoordinate("e1.iv.fresh", _clock.UtcNow.AddMinutes(-5), _clock.UtcNow.AddMinutes(-5));
        var prev = new GeoCoordinate("e1.iv.prev", _clock.UtcNow.AddMinutes(-1), _clock.UtcNow.AddMinutes(-1));

        var existing = BuildMember(current: prev, history: new[] { stale, fresh }, historyDurationMinutes: 15);
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns(existing);
        _tokenHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        Member? captured = null;
        CaptureMemberWrite(existing, m => captured = m);

        var svc = BuildService();
        await svc.UpdateLocationAsync("g1", "m1", "ok",
            new UpdateLocationRequest("e1.iv.new2", _clock.UtcNow), CancellationToken.None);

        captured!.RecentHistory.Should().Equal(fresh, prev); // stale dropped, oldest-first preserved
    }

    [Fact]
    public async Task UpdateLocation_WithZeroDuration_ClearsHistory()
    {
        var prev = new GeoCoordinate("e1.iv.prev", _clock.UtcNow.AddSeconds(-30), _clock.UtcNow.AddSeconds(-30));
        var existing = BuildMember(current: prev, history: Array.Empty<GeoCoordinate>(), historyDurationMinutes: 0);
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns(existing);
        _tokenHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        Member? captured = null;
        CaptureMemberWrite(existing, m => captured = m);

        var svc = BuildService();
        await svc.UpdateLocationAsync("g1", "m1", "ok",
            new UpdateLocationRequest("e1.iv.zero", _clock.UtcNow), CancellationToken.None);

        captured!.RecentHistory.Should().BeEmpty();
        captured.CurrentLocation!.EncryptedLocation.Should().Be("e1.iv.zero");
    }

    [Fact]
    public async Task UpdateLocation_HistoryClampedToHardCap()
    {
        var prev = new GeoCoordinate("e1.iv.prev", _clock.UtcNow, _clock.UtcNow);
        var bigHistory = Enumerable.Range(0, LocationService.HistoryHardCap + 50)
            .Select(i => new GeoCoordinate($"e1.iv.{i}", _clock.UtcNow.AddSeconds(-i), _clock.UtcNow.AddSeconds(-i)))
            .Reverse()
            .ToList();

        var existing = BuildMember(current: prev, history: bigHistory, historyDurationMinutes: 2880);
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns(existing);
        _tokenHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        Member? captured = null;
        CaptureMemberWrite(existing, m => captured = m);

        var svc = BuildService();
        await svc.UpdateLocationAsync("g1", "m1", "ok",
            new UpdateLocationRequest("e1.iv.cap", _clock.UtcNow), CancellationToken.None);

        captured!.RecentHistory.Should().HaveCount(LocationService.HistoryHardCap);
        captured.RecentHistory.Last().Should().Be(prev); // newest kept
    }

    [Fact]
    public async Task UpdateMemberSettings_PersistsDuration()
    {
        var existing = BuildMember(historyDurationMinutes: 15);
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns(existing);
        _tokenHasher.Verify("ok", "hash").Returns(true);

        Member? captured = null;
        CaptureMemberWrite(existing, m => captured = m);

        var svc = BuildService();
        await svc.UpdateMemberSettingsAsync("g1", "m1", "ok",
            new UpdateMemberSettingsRequest(60), CancellationToken.None);

        captured!.HistoryDurationMinutes.Should().Be(60);
    }

    [Fact]
    public async Task UpdateMemberSettings_RejectsBadToken()
    {
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns(BuildMember());
        _tokenHasher.Verify("bad", "hash").Returns(false);

        var svc = BuildService();
        var act = () => svc.UpdateMemberSettingsAsync("g1", "m1", "bad",
            new UpdateMemberSettingsRequest(30), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidTokenException>();
    }

    [Fact]
    public async Task DeleteHistory_ClearsHistoryAndCurrentLocation()
    {
        var current = new GeoCoordinate("e1.iv.cur", _clock.UtcNow, _clock.UtcNow);
        var history = new[] { new GeoCoordinate("e1.iv.hist", _clock.UtcNow.AddMinutes(-2), _clock.UtcNow.AddMinutes(-2)) };
        var existing = BuildMember(current: current, history: history);
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns(existing);
        _tokenHasher.Verify("ok", "hash").Returns(true);

        Member? captured = null;
        CaptureMemberWrite(existing, m => captured = m);

        var svc = BuildService();
        await svc.DeleteHistoryAsync("g1", "m1", "ok", CancellationToken.None);

        captured!.CurrentLocation.Should().BeNull();
        captured.RecentHistory.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteHistory_RejectsBadToken()
    {
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns(BuildMember());
        _tokenHasher.Verify("bad", "hash").Returns(false);

        var svc = BuildService();
        var act = () => svc.DeleteHistoryAsync("g1", "m1", "bad", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidTokenException>();
    }

    [Fact]
    public async Task GetLocations_Returns304_WhenVersionUnchanged()
    {
        _groupRepo.ReadGroupAsync("g1", Arg.Any<CancellationToken>())
            .Returns(new Group { GroupId = "g1", Name = "n", CreatedAt = _clock.UtcNow, Version = 7 });

        var svc = BuildService();
        var result = await svc.GetLocationsAsync("g1", sinceVersion: 7, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLocations_GroupMissing_Throws()
    {
        _groupRepo.ReadGroupAsync("missing", Arg.Any<CancellationToken>()).Returns((Group?)null);

        var svc = BuildService();
        var act = () => svc.GetLocationsAsync("missing", null, CancellationToken.None);

        await act.Should().ThrowAsync<GroupNotFoundException>();
    }

    [Fact]
    public async Task GetLocations_ReturnsMembers_WhenVersionChanged()
    {
        _groupRepo.ReadGroupAsync("g1", Arg.Any<CancellationToken>())
            .Returns(new Group { GroupId = "g1", Name = "n", CreatedAt = _clock.UtcNow, Version = 8 });

        var member = BuildMember(current: new GeoCoordinate("e1.iv.cur", _clock.UtcNow, _clock.UtcNow));
        _groupRepo.QueryMembersSinceAsync("g1", 5, Arg.Any<CancellationToken>())
            .Returns(ToAsync(new[] { member }));

        var svc = BuildService();
        var result = await svc.GetLocationsAsync("g1", sinceVersion: 5, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Version.Should().Be(8);
        result.Members.Should().HaveCount(1);
        result.Members[0].DisplayName.Should().Be("Antonin");
        result.Members[0].CurrentLocation!.EncryptedLocation.Should().Be("e1.iv.cur");
    }

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }
}
