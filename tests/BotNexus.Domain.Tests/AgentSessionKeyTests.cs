using System.Text.Json;
using BotNexus.Domain.Primitives;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class AgentSessionKeyTests
{
    [Fact]
    public void AgentSessionKey_From_WhenValuesAreValid_ShouldCreateInstance()
    {
        var key = AgentSessionKey.From(AgentId.From("agent-1"), SessionId.From("session-1"));
        key.AgentId.Value.Should().Be("agent-1");
    }

    [Fact]
    public void AgentSessionKey_Parse_WhenValueIsEmpty_ShouldThrowArgumentException()
    {
        var action = () => AgentSessionKey.Parse(" ");
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AgentSessionKey_Parse_WhenValueIsInvalidFormat_ShouldThrowArgumentException()
    {
        var action = () => AgentSessionKey.Parse("invalid");
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AgentSessionKey_Parse_WhenValueIsValid_ShouldCreateInstance()
    {
        var key = AgentSessionKey.Parse("agent-1::session-1");
        key.SessionId.Value.Should().Be("session-1");
    }

    [Fact]
    public void AgentSessionKey_Equals_WhenValuesMatch_ShouldBeTrue()
    {
        var left = AgentSessionKey.From(AgentId.From("agent-1"), SessionId.From("session-1"));
        var right = AgentSessionKey.From(AgentId.From("agent-1"), SessionId.From("session-1"));
        left.Should().Be(right);
    }

    [Fact]
    public void AgentSessionKey_Equals_WhenValuesDiffer_ShouldBeFalse()
    {
        var left = AgentSessionKey.From(AgentId.From("agent-1"), SessionId.From("session-1"));
        var right = AgentSessionKey.From(AgentId.From("agent-2"), SessionId.From("session-1"));
        left.Should().NotBe(right);
    }

    [Fact]
    public void AgentSessionKey_ToString_WhenCalled_ShouldReturnComposedValue()
    {
        var key = AgentSessionKey.From(AgentId.From("agent-1"), SessionId.From("session-1"));
        key.ToString().Should().Be("agent-1::session-1");
    }

    [Fact]
    public void AgentSessionKey_JsonRoundTrip_WhenSerializedAndDeserialized_ShouldBeEqual()
    {
        var original = AgentSessionKey.From(AgentId.From("agent-1"), SessionId.From("session-1"));
        var roundTrip = JsonSerializer.Deserialize<AgentSessionKey>(JsonSerializer.Serialize(original));
        roundTrip.Should().Be(original);
    }
}
