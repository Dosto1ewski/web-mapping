using FluentAssertions;
using Standort.Infrastructure.Security;

namespace Standort.UnitTests.Infrastructure;

public class Sha256TokenHasherTests
{
    private readonly Sha256TokenHasher _hasher = new();

    [Fact]
    public void Verify_ReturnsTrue_ForRoundtrippedToken()
    {
        var plain = "abc-this-is-a-token";
        var hash = _hasher.Hash(plain);

        _hasher.Verify(plain, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForMismatchedToken()
    {
        var hash = _hasher.Hash("real-token");

        _hasher.Verify("other-token", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForGarbageHash()
    {
        _hasher.Verify("any", "not-base64!!").Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForEmptyInputs()
    {
        _hasher.Verify("", "").Should().BeFalse();
        _hasher.Verify("token", "").Should().BeFalse();
        _hasher.Verify("", "hash").Should().BeFalse();
    }
}
