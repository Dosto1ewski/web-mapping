using FluentAssertions;
using Standort.Application.DTOs;
using Standort.Application.Validators;
using Standort.UnitTests.TestHelpers;

namespace Standort.UnitTests.Application;

public class CreateGroupRequestValidatorTests
{
    private readonly CreateGroupRequestValidator _v = new();

    [Fact]
    public void Valid_WhenAllFieldsOk()
    {
        var r = new CreateGroupRequest("Wandertour", "Antonin");
        _v.Validate(r).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_WhenNameEmpty()
    {
        _v.Validate(new CreateGroupRequest("", "Antonin")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_WhenDisplayNameTooLong()
    {
        var longName = new string('a', 33);
        _v.Validate(new CreateGroupRequest("g", longName)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_WhenDisplayNameHasIllegalChars()
    {
        _v.Validate(new CreateGroupRequest("g", "anto@nin")).IsValid.Should().BeFalse();
    }
}

public class JoinGroupRequestValidatorTests
{
    private readonly JoinGroupRequestValidator _v = new();

    [Theory]
    [InlineData("ABCD-1234", true)]
    [InlineData("0000-0000", true)]
    [InlineData("abcd-1234", false)] // lowercase invalid
    [InlineData("ABCDEFGH", false)]   // missing hyphen
    [InlineData("AILO-1234", false)]  // contains forbidden I, L, O
    public void InviteCodeFormat(string code, bool expectValid)
    {
        var result = _v.Validate(new JoinGroupRequest(code, "Mira"));
        result.IsValid.Should().Be(expectValid);
    }
}

public class UpdateLocationRequestValidatorTests
{
    private readonly FakeClock _clock = new() { UtcNow = new(2026, 5, 5, 10, 0, 0, TimeSpan.Zero) };

    [Fact]
    public void Valid_WhenAllFieldsInRange()
    {
        var v = new UpdateLocationRequestValidator(_clock);
        var r = new UpdateLocationRequest(49.0, 8.4, 18.0, _clock.UtcNow.AddSeconds(-5));
        v.Validate(r).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(91, 8.0)]
    [InlineData(-91, 8.0)]
    [InlineData(49.0, 181)]
    [InlineData(49.0, -181)]
    public void Invalid_WhenLatLngOutOfRange(double lat, double lng)
    {
        var v = new UpdateLocationRequestValidator(_clock);
        var r = new UpdateLocationRequest(lat, lng, 18.0, _clock.UtcNow);
        v.Validate(r).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_WhenAccuracyNegative()
    {
        var v = new UpdateLocationRequestValidator(_clock);
        var r = new UpdateLocationRequest(49.0, 8.4, -1, _clock.UtcNow);
        v.Validate(r).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_WhenRecordedAtTooOld()
    {
        var v = new UpdateLocationRequestValidator(_clock);
        var r = new UpdateLocationRequest(49.0, 8.4, 5, _clock.UtcNow.AddDays(-2));
        v.Validate(r).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_WhenRecordedAtTooFarInFuture()
    {
        var v = new UpdateLocationRequestValidator(_clock);
        var r = new UpdateLocationRequest(49.0, 8.4, 5, _clock.UtcNow.AddMinutes(5));
        v.Validate(r).IsValid.Should().BeFalse();
    }
}
