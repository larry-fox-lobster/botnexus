using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests;

/// <summary>
/// Unit tests for <see cref="AgentSessionManager"/> — focused on the new
/// <see cref="AgentSessionManager.ViewSubAgentAsync"/> method for read-only
/// sub-agent session viewing.
/// </summary>
public sealed class AgentSessionManagerTests : IDisposable
{
    private readonly AgentSessionManager _manager;

    public AgentSessionManagerTests()
    {
        _manager = TestSessionFactory.CreateManager();
    }

    public void Dispose()
    {
        _manager.Dispose();
    }

    // ── ViewSubAgentAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ViewSubAgentAsync_creates_new_session_state_for_new_subagent()
    {
        var subAgent = new SubAgentInfo
        {
            SubAgentId = "sub-12345",
            Name = "Explorer",
            Task = "Search codebase"
        };

        await _manager.ViewSubAgentAsync(subAgent);

        _manager.Sessions.ShouldContainKey("sub-12345");
    }

    [Fact]
    public async Task ViewSubAgentAsync_sets_SessionType_to_agent_subagent()
    {
        var subAgent = new SubAgentInfo
        {
            SubAgentId = "sub-12345",
            Name = "Explorer"
        };

        await _manager.ViewSubAgentAsync(subAgent);

        var state = _manager.Sessions["sub-12345"];
        state.SessionType.ShouldBe("agent-subagent");
    }

    [Fact]
    public async Task ViewSubAgentAsync_sets_IsReadOnly_to_true()
    {
        var subAgent = new SubAgentInfo
        {
            SubAgentId = "sub-12345",
            Name = "Explorer"
        };

        await _manager.ViewSubAgentAsync(subAgent);

        var state = _manager.Sessions["sub-12345"];
        state.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public async Task ViewSubAgentAsync_sets_DisplayName_from_SubAgentInfo_Name()
    {
        var subAgent = new SubAgentInfo
        {
            SubAgentId = "sub-12345",
            Name = "Code Explorer"
        };

        await _manager.ViewSubAgentAsync(subAgent);

        var state = _manager.Sessions["sub-12345"];
        state.DisplayName.ShouldBe("Code Explorer");
    }

    [Fact]
    public async Task ViewSubAgentAsync_generates_DisplayName_when_Name_is_null()
    {
        var subAgent = new SubAgentInfo
        {
            SubAgentId = "sub-12345678-long-id",
            Name = null
        };

        await _manager.ViewSubAgentAsync(subAgent);

        var state = _manager.Sessions["sub-12345678-long-id"];
        state.DisplayName.ShouldContain("Sub-agent");
        state.DisplayName.ShouldContain("sub-1234"); // First 8 chars of ID
    }

    [Fact]
    public async Task ViewSubAgentAsync_handles_short_SubAgentId()
    {
        var subAgent = new SubAgentInfo
        {
            SubAgentId = "sub",
            Name = null
        };

        await _manager.ViewSubAgentAsync(subAgent);

        var state = _manager.Sessions["sub"];
        state.DisplayName.ShouldContain("Sub-agent");
        state.DisplayName.ShouldContain("sub");
    }

    [Fact]
    public async Task ViewSubAgentAsync_sets_SessionId_to_SubAgentId()
    {
        var subAgent = new SubAgentInfo
        {
            SubAgentId = "sub-12345",
            Name = "Explorer"
        };

        await _manager.ViewSubAgentAsync(subAgent);

        var state = _manager.Sessions["sub-12345"];
        state.SessionId.ShouldBe("sub-12345");
    }

    [Fact]
    public async Task ViewSubAgentAsync_sets_AgentId_to_SubAgentId()
    {
        var subAgent = new SubAgentInfo
        {
            SubAgentId = "sub-12345",
            Name = "Explorer"
        };

        await _manager.ViewSubAgentAsync(subAgent);

        var state = _manager.Sessions["sub-12345"];
        state.AgentId.ShouldBe("sub-12345");
    }

    [Fact]
    public async Task ViewSubAgentAsync_sets_IsConnected_to_true()
    {
        var subAgent = new SubAgentInfo
        {
            SubAgentId = "sub-12345",
            Name = "Explorer"
        };

        await _manager.ViewSubAgentAsync(subAgent);

        var state = _manager.Sessions["sub-12345"];
        state.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public async Task ViewSubAgentAsync_reuses_existing_session_state()
    {
        var subAgent = new SubAgentInfo
        {
            SubAgentId = "sub-12345",
            Name = "Explorer"
        };

        // First call creates the session
        await _manager.ViewSubAgentAsync(subAgent);
        var firstState = _manager.Sessions["sub-12345"];
        firstState.Messages.Add(new ChatMessage("User", "Test message", DateTimeOffset.UtcNow));

        // Second call reuses the same session
        await _manager.ViewSubAgentAsync(subAgent);
        var secondState = _manager.Sessions["sub-12345"];

        secondState.ShouldBe(firstState);
        secondState.Messages.Count.ShouldBe(1);
        secondState.Messages[0].Content.ShouldBe("Test message");
    }

    [Fact]
    public async Task ViewSubAgentAsync_sets_ActiveAgentId()
    {
        var subAgent = new SubAgentInfo
        {
            SubAgentId = "sub-12345",
            Name = "Explorer"
        };

        await _manager.ViewSubAgentAsync(subAgent);

        _manager.ActiveAgentId.ShouldBe("sub-12345");
    }

    [Fact]
    public async Task ViewSubAgentAsync_switches_ActiveAgentId_on_subsequent_calls()
    {
        var firstSubAgent = new SubAgentInfo { SubAgentId = "sub-1", Name = "First" };
        var secondSubAgent = new SubAgentInfo { SubAgentId = "sub-2", Name = "Second" };

        await _manager.ViewSubAgentAsync(firstSubAgent);
        _manager.ActiveAgentId.ShouldBe("sub-1");

        await _manager.ViewSubAgentAsync(secondSubAgent);
        _manager.ActiveAgentId.ShouldBe("sub-2");
    }
}
