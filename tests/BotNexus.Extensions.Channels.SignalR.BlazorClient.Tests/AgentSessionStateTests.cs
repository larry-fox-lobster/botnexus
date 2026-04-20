using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests;

/// <summary>
/// Unit tests for <see cref="AgentSessionState"/> — focused on the new
/// read-only sub-agent session behavior (SessionType and IsReadOnly).
/// </summary>
public sealed class AgentSessionStateTests
{
    // ── SessionType property ──────────────────────────────────────────────

    [Fact]
    public void SessionType_defaults_to_user_agent()
    {
        var state = new AgentSessionState { AgentId = "test-agent" };

        state.SessionType.ShouldBe("user-agent");
    }

    [Fact]
    public void SessionType_can_be_set_to_agent_subagent()
    {
        var state = new AgentSessionState { AgentId = "test-agent", SessionType = "agent-subagent" };

        state.SessionType.ShouldBe("agent-subagent");
    }

    [Fact]
    public void SessionType_can_be_set_to_custom_value()
    {
        var state = new AgentSessionState { AgentId = "test-agent", SessionType = "custom-session-type" };

        state.SessionType.ShouldBe("custom-session-type");
    }

    // ── IsReadOnly computed property ──────────────────────────────────────

    [Fact]
    public void IsReadOnly_returns_true_when_SessionType_is_agent_subagent()
    {
        var state = new AgentSessionState { AgentId = "test-agent", SessionType = "agent-subagent" };

        state.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void IsReadOnly_returns_false_when_SessionType_is_user_agent()
    {
        var state = new AgentSessionState { AgentId = "test-agent", SessionType = "user-agent" };

        state.IsReadOnly.ShouldBeFalse();
    }

    [Fact]
    public void IsReadOnly_returns_false_when_SessionType_is_default()
    {
        var state = new AgentSessionState { AgentId = "test-agent" };

        state.IsReadOnly.ShouldBeFalse();
    }

    [Fact]
    public void IsReadOnly_returns_false_for_custom_session_types()
    {
        var state = new AgentSessionState { AgentId = "test-agent", SessionType = "custom-type" };

        state.IsReadOnly.ShouldBeFalse();
    }

    [Fact]
    public void IsReadOnly_is_case_sensitive()
    {
        var stateUpperCase = new AgentSessionState { AgentId = "test-agent", SessionType = "AGENT-SUBAGENT" };
        var stateMixedCase = new AgentSessionState { AgentId = "test-agent", SessionType = "Agent-SubAgent" };

        stateUpperCase.IsReadOnly.ShouldBeFalse();
        stateMixedCase.IsReadOnly.ShouldBeFalse();
    }

    // ── SessionType mutation updates IsReadOnly ───────────────────────────

    [Fact]
    public void IsReadOnly_updates_when_SessionType_changes_to_agent_subagent()
    {
        var state = new AgentSessionState { AgentId = "test-agent", SessionType = "user-agent" };
        state.IsReadOnly.ShouldBeFalse();

        state.SessionType = "agent-subagent";

        state.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void IsReadOnly_updates_when_SessionType_changes_from_agent_subagent()
    {
        var state = new AgentSessionState { AgentId = "test-agent", SessionType = "agent-subagent" };
        state.IsReadOnly.ShouldBeTrue();

        state.SessionType = "user-agent";

        state.IsReadOnly.ShouldBeFalse();
    }
}
