using System.Text.Json;
using BotNexus.Domain.Primitives;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class ChannelKeyTests
{
    [Fact]
    public void ChannelKey_From_WhenValueIsValid_ShouldCreateInstance()
    {
        var result = ChannelKey.From("signalr");
        result.Value.Should().Be("signalr");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ChannelKey_From_WhenValueIsEmpty_ShouldThrowArgumentException(string? value)
    {
        var action = () => ChannelKey.From(value!);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChannelKey_Constructor_WhenValueHasMixedCase_ShouldNormalizeToLowercase()
    {
        var result = new ChannelKey("  SiGnAlR ");
        result.Value.Should().Be("signalr");
    }

    [Fact]
    public void ChannelKey_Equals_WhenValuesMatch_ShouldBeTrue()
    {
        var left = ChannelKey.From("SignalR");
        var right = ChannelKey.From("signalr");
        left.Should().Be(right);
    }

    [Fact]
    public void ChannelKey_Equals_WhenValuesDiffer_ShouldBeFalse()
    {
        var left = ChannelKey.From("signalr");
        var right = ChannelKey.From("telegram");
        left.Should().NotBe(right);
    }

    [Fact]
    public void ChannelKey_From_WebChatAlias_ShouldResolveToSignalr()
    {
        var alias = ChannelKey.From("web chat");
        var canonical = ChannelKey.From("signalr");
        alias.Should().Be(canonical);
    }

    [Fact]
    public void ChannelKey_From_HyphenatedWebChatAlias_ShouldResolveToSignalr()
    {
        var alias = ChannelKey.From("Web-Chat");
        var canonical = ChannelKey.From("signalr");
        alias.Should().Be(canonical);
    }

    [Fact]
    public void ChannelKey_ImplicitConversion_WhenConvertedToString_ShouldReturnValue()
    {
        var channelKey = ChannelKey.From("SignalR");
        string value = channelKey;
        value.Should().Be("signalr");
    }

    [Fact]
    public void ChannelKey_ExplicitConversion_WhenConvertedFromString_ShouldCreateInstance()
    {
        var channelKey = (ChannelKey)" SignalR ";
        channelKey.Value.Should().Be("signalr");
    }

    [Fact]
    public void ChannelKey_ToString_WhenCalled_ShouldReturnValue()
    {
        var channelKey = ChannelKey.From("SignalR");
        channelKey.ToString().Should().Be("signalr");
    }

    [Fact]
    public void ChannelKey_JsonRoundTrip_WhenSerializedAndDeserialized_ShouldBeEqual()
    {
        var original = ChannelKey.From("SignalR");
        var roundTrip = JsonSerializer.Deserialize<ChannelKey>(JsonSerializer.Serialize(original));
        roundTrip.Should().Be(original);
    }
}
