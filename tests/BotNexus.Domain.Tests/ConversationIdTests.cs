using System.Text.Json;
using BotNexus.Domain.Primitives;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class ConversationIdTests
{
    [Fact]
    public void ConversationId_From_WhenValueIsValid_ShouldCreateInstance()
    {
        var result = ConversationId.From(" conversation-1 ");
        result.Value.Should().Be("conversation-1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConversationId_From_WhenValueIsEmpty_ShouldThrowArgumentException(string? value)
    {
        var action = () => ConversationId.From(value!);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConversationId_Equals_WhenValuesMatch_ShouldBeTrue()
    {
        var left = ConversationId.From("conversation-1");
        var right = ConversationId.From("conversation-1");
        left.Should().Be(right);
    }

    [Fact]
    public void ConversationId_Equals_WhenValuesDiffer_ShouldBeFalse()
    {
        var left = ConversationId.From("conversation-1");
        var right = ConversationId.From("conversation-2");
        left.Should().NotBe(right);
    }

    [Fact]
    public void ConversationId_ImplicitConversion_WhenConvertedToString_ShouldReturnValue()
    {
        var id = ConversationId.From("conversation-1");
        string value = id;
        value.Should().Be("conversation-1");
    }

    [Fact]
    public void ConversationId_ExplicitConversion_WhenConvertedFromString_ShouldCreateInstance()
    {
        var id = (ConversationId)"conversation-1";
        id.Value.Should().Be("conversation-1");
    }

    [Fact]
    public void ConversationId_ToString_WhenCalled_ShouldReturnValue()
    {
        var id = ConversationId.From("conversation-1");
        id.ToString().Should().Be("conversation-1");
    }

    [Fact]
    public void ConversationId_JsonRoundTrip_WhenSerializedAndDeserialized_ShouldBeEqual()
    {
        var original = ConversationId.From("conversation-1");
        var roundTrip = JsonSerializer.Deserialize<ConversationId>(JsonSerializer.Serialize(original));
        roundTrip.Should().Be(original);
    }
}
