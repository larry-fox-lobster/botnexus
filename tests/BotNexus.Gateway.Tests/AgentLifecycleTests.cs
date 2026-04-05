using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotNexus.Gateway.Tests;

public class DefaultAgentRegistryTests
{
    [Fact]
    public void Register_WithValidDescriptor_AddsAgent()
    {
        var registry = CreateRegistry();
        var descriptor = CreateDescriptor("agent-a");

        registry.Register(descriptor);

        registry.Get("agent-a").Should().Be(descriptor);
    }

    [Fact]
    public void Unregister_WithKnownAgent_RemovesAgent()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("agent-a"));

        registry.Unregister("agent-a");

        registry.Contains("agent-a").Should().BeFalse();
    }

    [Fact]
    public void Register_WithDuplicateAgentId_ThrowsInvalidOperationException()
    {
        var registry = CreateRegistry();
        var descriptor = CreateDescriptor("agent-a");
        registry.Register(descriptor);

        var act = () => registry.Register(CreateDescriptor("agent-a"));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Get_WithUnknownAgentId_ReturnsNull()
    {
        var registry = CreateRegistry();

        var agent = registry.Get("unknown");

        agent.Should().BeNull();
    }

    [Fact]
    public void GetAll_WithMultipleAgents_ReturnsAllRegisteredAgents()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("agent-a"));
        registry.Register(CreateDescriptor("agent-b"));

        var agents = registry.GetAll();

        agents.Should().HaveCount(2);
    }

    [Fact]
    public void Contains_WithRegisteredAndUnknownIds_ReportsCorrectMembership()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("agent-a"));

        var contains = registry.Contains("agent-a") && !registry.Contains("unknown");

        contains.Should().BeTrue();
    }

    [Fact]
    public async Task Register_AndRead_FromConcurrentCalls_RemainsConsistent()
    {
        var registry = CreateRegistry();
        const int agentCount = 100;

        var tasks = Enumerable.Range(0, agentCount)
            .Select(i => Task.Run(() =>
            {
                var agentId = $"agent-{i}";
                registry.Register(CreateDescriptor(agentId));
                _ = registry.Get(agentId);
                _ = registry.Contains(agentId);
            }));

        await Task.WhenAll(tasks);

        registry.GetAll().Should().HaveCount(agentCount);
    }

    private static DefaultAgentRegistry CreateRegistry()
        => new(NullLogger<DefaultAgentRegistry>.Instance);

    private static AgentDescriptor CreateDescriptor(string agentId)
        => new()
        {
            AgentId = agentId,
            DisplayName = $"{agentId}-display",
            ModelId = "test-model",
            ApiProvider = "test-provider"
        };
}
