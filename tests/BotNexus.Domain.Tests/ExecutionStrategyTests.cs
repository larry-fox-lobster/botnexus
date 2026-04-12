using System.Text.Json;
using BotNexus.Domain.Primitives;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class ExecutionStrategyTests
{
    [Fact]
    public void ExecutionStrategy_KnownValues_WhenAccessed_ShouldExist()
    {
        ExecutionStrategy.InProcess.Value.Should().Be("in-process");
    }

    [Fact]
    public void ExecutionStrategy_FromString_WhenValueIsKnown_ShouldReturnKnownInstance()
    {
        var strategy = ExecutionStrategy.FromString("IN-PROCESS");
        strategy.Should().BeSameAs(ExecutionStrategy.InProcess);
    }

    [Fact]
    public void ExecutionStrategy_FromString_WhenValueIsUnknown_ShouldCreateExtensibleInstance()
    {
        var strategy = ExecutionStrategy.FromString("distributed-mesh");
        strategy.Value.Should().Be("distributed-mesh");
    }

    [Fact]
    public void ExecutionStrategy_FromString_WhenValueHasDifferentCase_ShouldMatchCaseInsensitively()
    {
        var first = ExecutionStrategy.FromString("CUSTOM-STRATEGY");
        var second = ExecutionStrategy.FromString("custom-strategy");
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void ExecutionStrategy_ImplicitConversion_WhenConvertedToString_ShouldReturnValue()
    {
        string value = ExecutionStrategy.Remote;
        value.Should().Be("remote");
    }

    [Fact]
    public void ExecutionStrategy_Equals_WhenValuesMatch_ShouldBeTrue()
    {
        var left = ExecutionStrategy.FromString("sandbox");
        var right = ExecutionStrategy.Sandbox;
        left.Should().Be(right);
    }

    [Fact]
    public void ExecutionStrategy_Equals_WhenValuesDiffer_ShouldBeFalse()
    {
        var left = ExecutionStrategy.Container;
        var right = ExecutionStrategy.Remote;
        left.Should().NotBe(right);
    }

    [Fact]
    public void ExecutionStrategy_JsonRoundTrip_WhenSerializedAndDeserialized_ShouldBeEqual()
    {
        var roundTrip = JsonSerializer.Deserialize<ExecutionStrategy>(JsonSerializer.Serialize(ExecutionStrategy.Container));
        roundTrip.Should().Be(ExecutionStrategy.Container);
    }

    [Fact]
    public async Task ExecutionStrategy_FromString_WhenCalledConcurrently_ShouldBeThreadSafe()
    {
        var tasks = Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() => ExecutionStrategy.FromString("thread-strategy")))
            .ToArray();
        var results = await Task.WhenAll(tasks);
        results.Distinct().Should().HaveCount(1);
    }
}
