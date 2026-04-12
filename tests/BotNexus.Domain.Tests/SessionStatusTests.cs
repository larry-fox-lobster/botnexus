using System.Text.Json;
using BotNexus.Domain.Primitives;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class SessionStatusTests
{
    [Fact]
    public void SessionStatus_KnownValues_WhenAccessed_ShouldExist()
    {
        SessionStatus.Active.Value.Should().Be("active");
    }

    [Fact]
    public void SessionStatus_FromString_WhenValueIsKnown_ShouldReturnKnownInstance()
    {
        var status = SessionStatus.FromString("ACTIVE");
        status.Should().BeSameAs(SessionStatus.Active);
    }

    [Fact]
    public void SessionStatus_FromString_WhenValueIsUnknown_ShouldCreateExtensibleInstance()
    {
        var status = SessionStatus.FromString("paused-for-maintenance");
        status.Value.Should().Be("paused-for-maintenance");
    }

    [Fact]
    public void SessionStatus_FromString_WhenValueHasDifferentCase_ShouldMatchCaseInsensitively()
    {
        var first = SessionStatus.FromString("CUSTOM-STATUS");
        var second = SessionStatus.FromString("custom-status");
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void SessionStatus_ImplicitConversion_WhenConvertedToString_ShouldReturnValue()
    {
        string value = SessionStatus.Sealed;
        value.Should().Be("sealed");
    }

    [Fact]
    public void SessionStatus_Equals_WhenValuesMatch_ShouldBeTrue()
    {
        var left = SessionStatus.FromString("suspended");
        var right = SessionStatus.Suspended;
        left.Should().Be(right);
    }

    [Fact]
    public void SessionStatus_Equals_WhenValuesDiffer_ShouldBeFalse()
    {
        var left = SessionStatus.Active;
        var right = SessionStatus.Sealed;
        left.Should().NotBe(right);
    }

    [Fact]
    public void SessionStatus_JsonRoundTrip_WhenSerializedAndDeserialized_ShouldBeEqual()
    {
        var roundTrip = JsonSerializer.Deserialize<SessionStatus>(JsonSerializer.Serialize(SessionStatus.Suspended));
        roundTrip.Should().Be(SessionStatus.Suspended);
    }

    [Fact]
    public async Task SessionStatus_FromString_WhenCalledConcurrently_ShouldBeThreadSafe()
    {
        var tasks = Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() => SessionStatus.FromString("thread-status")))
            .ToArray();
        var results = await Task.WhenAll(tasks);
        results.Distinct().Should().HaveCount(1);
    }
}
