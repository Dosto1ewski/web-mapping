using FluentAssertions;
using Standort.Application.Identity;

namespace Standort.UnitTests.Application;

public class MemberIdFactoryTests
{
    [Fact]
    public void FromGroupAndNormalizedName_IsDeterministic()
    {
        var a = MemberIdFactory.FromGroupAndNormalizedName("g1", "antonin");
        var b = MemberIdFactory.FromGroupAndNormalizedName("g1", "antonin");
        a.Should().Be(b);
    }

    [Fact]
    public void FromGroupAndNormalizedName_DiffersByGroup()
    {
        MemberIdFactory.FromGroupAndNormalizedName("g1", "antonin")
            .Should().NotBe(MemberIdFactory.FromGroupAndNormalizedName("g2", "antonin"));
    }

    [Fact]
    public void FromGroupAndNormalizedName_DiffersByName()
    {
        MemberIdFactory.FromGroupAndNormalizedName("g1", "antonin")
            .Should().NotBe(MemberIdFactory.FromGroupAndNormalizedName("g1", "mira"));
    }

    [Fact]
    public void FromGroupAndNormalizedName_OutputIs16CrockfordChars()
    {
        var id = MemberIdFactory.FromGroupAndNormalizedName("group-id", "anyone");
        id.Should().HaveLength(16);
        id.Should().MatchRegex("^[0-9A-HJ-NP-TV-Z]{16}$");
    }
}

public class DisplayNameNormalizerTests
{
    [Theory]
    [InlineData("Antonin", "antonin")]
    [InlineData("  Mira  ", "mira")]
    [InlineData("MIXED Case", "mixed case")]
    public void Normalize_TrimsAndLowercases(string input, string expected)
    {
        DisplayNameNormalizer.Normalize(input).Should().Be(expected);
    }
}
