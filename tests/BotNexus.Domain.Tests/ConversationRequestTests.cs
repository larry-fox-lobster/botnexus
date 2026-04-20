using BotNexus.Domain.Conversations;
using BotNexus.Domain.Primitives;

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

        request.MaxTurns.ShouldBe(1);
        request.CallChain.ShouldBeEmpty();
        request.Objective.ShouldBeNull();
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

        request.MaxTurns.ShouldBe(4);
        request.Objective.ShouldBe("finish task");
        request.CallChain.Select(agent => agent.Value).ShouldBe(new[] { "agent-a", "agent-c" });
    }
}
