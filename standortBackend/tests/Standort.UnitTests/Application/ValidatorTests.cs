using FluentAssertions;
using Standort.Application.DTOs;
using Standort.Application.Validators;
using Standort.UnitTests.TestHelpers;

namespace Standort.UnitTests.Application;

public class CreateGroupRequestValidatorTests
{
    private readonly CreateGroupRequestValidator _v = new();

    private static readonly string ValidHash = new string('a', 64);

    [Fact]
    public void Valid_WhenAllFieldsOk()
    {
        var r = new CreateGroupRequest("Wandertour", "Antonin", ValidHash);
        _v.Validate(r).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_WhenNameEmpty()
    {
        _v.Validate(new CreateGroupRequest("", "Antonin", ValidHash)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_WhenDisplayNameTooLong()
    {
        var longName = new string('a', 33);
        _v.Validate(new CreateGroupRequest("g", longName, ValidHash)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_WhenDisplayNameHasIllegalChars()
    {
        _v.Validate(new CreateGroupRequest("g", "anto@nin", ValidHash)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_WhenHashNotHex()
    {
        _v.Validate(new CreateGroupRequest("g", "Antonin", new string('z', 64))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_WhenHashWrongLength()
    {
        _v.Validate(new CreateGroupRequest("g", "Antonin", "abc123")).IsValid.Should().BeFalse();
    }
}

public class JoinGroupRequestValidatorTests
{
    private readonly JoinGroupRequestValidator _v = new();

    [Theory]
    [InlineData("ABCD1234-EFGH5678", true)]
    [InlineData("00000000-00000000", true)]
    [InlineData("abcd1234-efgh5678", false)] // lowercase invalid
    [InlineData("ABCD1234EFGH5678", false)]  // missing hyphen
    [InlineData("ABCD-1234", false)]         // old 4+4 format no longer valid
    [InlineData("AILOOULO-AILOOULO", false)] // contains forbidden I, L, O, U
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
    public void Valid_WhenEncryptedLocationPresent()
    {
        var v = new UpdateLocationRequestValidator(_clock);
        var r = new UpdateLocationRequest("e1.someIv.someCiphertext", _clock.UtcNow.AddSeconds(-5));
        v.Validate(r).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_WhenEncryptedLocationEmpty()
    {
        var v = new UpdateLocationRequestValidator(_clock);
        var r = new UpdateLocationRequest("", _clock.UtcNow);
        v.Validate(r).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_WhenRecordedAtTooOld()
    {
        var v = new UpdateLocationRequestValidator(_clock);
        var r = new UpdateLocationRequest("e1.iv.ct", _clock.UtcNow.AddDays(-2));
        v.Validate(r).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_WhenRecordedAtTooFarInFuture()
    {
        var v = new UpdateLocationRequestValidator(_clock);
        var r = new UpdateLocationRequest("e1.iv.ct", _clock.UtcNow.AddMinutes(5));
        v.Validate(r).IsValid.Should().BeFalse();
    }
}
