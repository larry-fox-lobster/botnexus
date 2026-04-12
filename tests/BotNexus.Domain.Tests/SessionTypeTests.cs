using System.Text.Json;
using BotNexus.Domain.Primitives;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class SessionTypeTests
{
    [Fact]
    public void SessionType_KnownValues_WhenAccessed_ShouldExist()
    {
        SessionType.UserAgent.Value.Should().Be("user-agent");
    }

    [Fact]
    public void SessionType_FromString_WhenValueIsKnown_ShouldReturnKnownInstance()
    {
        var type = SessionType.FromString("USER-AGENT");
        type.Should().BeSameAs(SessionType.UserAgent);
    }

    [Fact]
    public void SessionType_FromString_WhenValueIsUnknown_ShouldCreateExtensibleInstance()
    {
        var type = SessionType.FromString("internal-trigger");
        type.Value.Should().Be("internal-trigger");
    }

    [Fact]
    public void SessionType_FromString_WhenValueHasDifferentCase_ShouldMatchCaseInsensitively()
    {
        var first = SessionType.FromString("CUSTOM-TYPE");
        var second = SessionType.FromString("custom-type");
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void SessionType_ImplicitConversion_WhenConvertedToString_ShouldReturnValue()
    {
        string value = SessionType.Cron;
        value.Should().Be("cron");
    }

    [Fact]
    public void SessionType_Equals_WhenValuesMatch_ShouldBeTrue()
    {
        var left = SessionType.FromString("agent-subagent");
        var right = SessionType.AgentSubAgent;
        left.Should().Be(right);
    }

    [Fact]
    public void SessionType_Equals_WhenValuesDiffer_ShouldBeFalse()
    {
        var left = SessionType.UserAgent;
        var right = SessionType.AgentSelf;
        left.Should().NotBe(right);
    }

    [Fact]
    public void SessionType_JsonRoundTrip_WhenSerializedAndDeserialized_ShouldBeEqual()
    {
        var roundTrip = JsonSerializer.Deserialize<SessionType>(JsonSerializer.Serialize(SessionType.AgentAgent));
        roundTrip.Should().Be(SessionType.AgentAgent);
    }

    [Fact]
    public async Task SessionType_FromString_WhenCalledConcurrently_ShouldBeThreadSafe()
    {
        var tasks = Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() => SessionType.FromString("thread-type")))
            .ToArray();
        var results = await Task.WhenAll(tasks);
        results.Distinct().Should().HaveCount(1);
    }
}
