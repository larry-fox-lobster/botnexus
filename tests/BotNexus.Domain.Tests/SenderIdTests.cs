using System.Text.Json;
using BotNexus.Domain.Primitives;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class SenderIdTests
{
    [Fact]
    public void SenderId_From_WhenValueIsValid_ShouldCreateInstance()
    {
        var result = SenderId.From(" sender-1 ");
        result.Value.Should().Be("sender-1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SenderId_From_WhenValueIsEmpty_ShouldThrowArgumentException(string? value)
    {
        var action = () => SenderId.From(value!);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SenderId_Equals_WhenValuesMatch_ShouldBeTrue()
    {
        var left = SenderId.From("sender-1");
        var right = SenderId.From("sender-1");
        left.Should().Be(right);
    }

    [Fact]
    public void SenderId_Equals_WhenValuesDiffer_ShouldBeFalse()
    {
        var left = SenderId.From("sender-1");
        var right = SenderId.From("sender-2");
        left.Should().NotBe(right);
    }

    [Fact]
    public void SenderId_ImplicitConversion_WhenConvertedToString_ShouldReturnValue()
    {
        var id = SenderId.From("sender-1");
        string value = id;
        value.Should().Be("sender-1");
    }

    [Fact]
    public void SenderId_ExplicitConversion_WhenConvertedFromString_ShouldCreateInstance()
    {
        var id = (SenderId)"sender-1";
        id.Value.Should().Be("sender-1");
    }

    [Fact]
    public void SenderId_ToString_WhenCalled_ShouldReturnValue()
    {
        var id = SenderId.From("sender-1");
        id.ToString().Should().Be("sender-1");
    }

    [Fact]
    public void SenderId_JsonRoundTrip_WhenSerializedAndDeserialized_ShouldBeEqual()
    {
        var original = SenderId.From("sender-1");
        var roundTrip = JsonSerializer.Deserialize<SenderId>(JsonSerializer.Serialize(original));
        roundTrip.Should().Be(original);
    }
}
