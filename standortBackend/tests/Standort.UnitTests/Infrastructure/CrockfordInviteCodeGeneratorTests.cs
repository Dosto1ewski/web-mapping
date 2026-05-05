using FluentAssertions;
using Standort.Infrastructure.Security;

namespace Standort.UnitTests.Infrastructure;

public class CrockfordInviteCodeGeneratorTests
{
    private readonly CrockfordInviteCodeGenerator _gen = new();

    [Fact]
    public void GenerateInviteCode_FormatXxxxXxxx()
    {
        for (var i = 0; i < 100; i++)
        {
            var code = _gen.GenerateInviteCode();
            code.Should().HaveLength(9);
            code[4].Should().Be('-');
            code.Replace("-", "").Should().MatchRegex("^[0-9A-HJ-NP-TV-Z]{8}$");
        }
    }

    [Fact]
    public void GenerateInviteCode_ProducesVariation()
    {
        var codes = Enumerable.Range(0, 50).Select(_ => _gen.GenerateInviteCode()).ToHashSet();
        codes.Should().HaveCount(50, "50 random 8-char Crockford codes should not collide");
    }
}
