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

    private Member BuildMember(GeoCoordinate? current = null, IReadOnlyList<GeoCoordinate>? history = null) => new()
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
    };

    [Fact]
    public async Task UpdateLocation_RejectsBadToken()
    {
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns(BuildMember());
        _tokenHasher.Verify("bad", "hash").Returns(false);

        var svc = BuildService();
        var act = () => svc.UpdateLocationAsync("g1", "m1", "bad",
            new UpdateLocationRequest(49.0, 8.4, 10, _clock.UtcNow), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidTokenException>();
    }

    [Fact]
    public async Task UpdateLocation_MemberMissing_Throws()
    {
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns((Member?)null);

        var svc = BuildService();
        var act = () => svc.UpdateLocationAsync("g1", "m1", "any",
            new UpdateLocationRequest(0, 0, 0, _clock.UtcNow), CancellationToken.None);

        await act.Should().ThrowAsync<MemberNotFoundException>();
    }

    [Fact]
    public async Task UpdateLocation_PreviousCurrent_BecomesNewestHistoryEntry()
    {
        var prevCurrent = new GeoCoordinate(49.0, 8.4, 10, _clock.UtcNow.AddMinutes(-1), _clock.UtcNow.AddMinutes(-1));
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
            new UpdateLocationRequest(49.1, 8.5, 12, _clock.UtcNow), CancellationToken.None);

        captured!.CurrentLocation!.Lat.Should().Be(49.1);
        captured.RecentHistory.Should().HaveCount(1);
        captured.RecentHistory[0].Should().Be(prevCurrent);
    }

    [Fact]
    public async Task UpdateLocation_HistoryCappedAtFive()
    {
        var prev = new GeoCoordinate(49.0, 8.4, 10, _clock.UtcNow.AddMinutes(-1), _clock.UtcNow.AddMinutes(-1));
        var fullHistory = Enumerable.Range(0, 5)
            .Select(i => new GeoCoordinate(40 + i, 8, 10, _clock.UtcNow.AddMinutes(-10 + i), _clock.UtcNow.AddMinutes(-10 + i)))
            .ToList();

        var existing = BuildMember(current: prev, history: fullHistory);
        _groupRepo.ReadMemberAsync("g1", "m1", Arg.Any<CancellationToken>()).Returns(existing);
        _tokenHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        Member? captured = null;
        _groupRepo.ApplyMemberWriteAsync("g1", "m1",
                Arg.Any<Func<Group, Member?, Member>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var mutator = ci.Arg<Func<Group, Member?, Member>>();
                var grp = new Group { GroupId = "g1", Name = "n", CreatedAt = _clock.UtcNow, Version = 5 };
                captured = mutator(grp, existing);
                return Task.FromResult(captured);
            });

        var svc = BuildService();
        await svc.UpdateLocationAsync("g1", "m1", "ok",
            new UpdateLocationRequest(50, 9, 12, _clock.UtcNow), CancellationToken.None);

        captured!.RecentHistory.Should().HaveCount(LocationService.RecentHistoryMax);
        // Oldest entry of fullHistory should have been dropped; newest history entry is `prev`.
        captured.RecentHistory.Last().Should().Be(prev);
        captured.RecentHistory.Should().NotContain(fullHistory[0]); // dropped
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

        var member = BuildMember(current: new GeoCoordinate(49, 8, 10, _clock.UtcNow, _clock.UtcNow));
        _groupRepo.QueryMembersSinceAsync("g1", 5, Arg.Any<CancellationToken>())
            .Returns(ToAsync(new[] { member }));

        var svc = BuildService();
        var result = await svc.GetLocationsAsync("g1", sinceVersion: 5, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Version.Should().Be(8);
        result.Members.Should().HaveCount(1);
        result.Members[0].DisplayName.Should().Be("Antonin");
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
