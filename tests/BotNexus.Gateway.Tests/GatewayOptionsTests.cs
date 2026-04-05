using BotNexus.Gateway.Configuration;
using FluentAssertions;

namespace BotNexus.Gateway.Tests;

public sealed class GatewayOptionsTests
{
    [Fact]
    public void DefaultAgentId_CanBeNull()
    {
        var options = new GatewayOptions();

        options.DefaultAgentId.Should().BeNull();
    }

    [Fact]
    public void DefaultAgentId_CanBeAssigned()
    {
        var options = new GatewayOptions
        {
            DefaultAgentId = "agent-a"
        };

        options.DefaultAgentId.Should().Be("agent-a");
    }
}
