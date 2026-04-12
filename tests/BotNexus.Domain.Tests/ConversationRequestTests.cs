using BotNexus.Domain.Conversations;
using BotNexus.Domain.Primitives;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class ConversationRequestTests
{
    [Fact]
    public void ConversationRequest_Defaults_MaxTurnsAndCallChainAreSafe()
    {
        var request = new ConversationRequest
        {
            InitiatorId = AgentId.From("agent-a"),
            TargetId = AgentId.From("agent-b"),
            Message = "hello"
        };

        request.MaxTurns.Should().Be(1);
        request.CallChain.Should().BeEmpty();
        request.Objective.Should().BeNull();
    }

    [Fact]
    public void ConversationRequest_AllowsExplicitCallChainAndTurnLimit()
    {
        var request = new ConversationRequest
        {
            InitiatorId = AgentId.From("agent-a"),
            TargetId = AgentId.From("agent-b"),
            Message = "delegate this",
            Objective = "finish task",
            MaxTurns = 4,
            CallChain = [AgentId.From("agent-a"), AgentId.From("agent-c")]
        };

        request.MaxTurns.Should().Be(4);
        request.Objective.Should().Be("finish task");
        request.CallChain.Select(agent => agent.Value).Should().Equal("agent-a", "agent-c");
    }
}
