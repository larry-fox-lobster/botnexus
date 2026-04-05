using System.Text.RegularExpressions;
using BotNexus.Providers.Core.Utilities;
using FluentAssertions;

namespace BotNexus.Providers.Core.Tests.Utilities;

public class ShortHashTests
{
    [Fact]
    public void Generate_WithSameInput_ReturnsDeterministicHash()
    {
        var first = ShortHash.Generate("toolu_abc123");
        var second = ShortHash.Generate("toolu_abc123");

        first.Should().Be(second);
    }

    [Fact]
    public void Generate_ReturnsExpectedLength()
    {
        var hash = ShortHash.Generate("toolu_abc123");

        hash.Should().HaveLength(9);
    }

    [Fact]
    public void Generate_ReturnsOnlyAlphanumericCharacters()
    {
        var hash = ShortHash.Generate("toolu_abc123");

        Regex.IsMatch(hash, "^[a-zA-Z0-9]+$").Should().BeTrue();
    }
}
