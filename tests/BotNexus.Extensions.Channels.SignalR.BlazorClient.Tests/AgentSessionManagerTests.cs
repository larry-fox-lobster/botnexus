using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;
using System.Net;
using System.Text;
using System.Text.Json;

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

/// <summary>
/// Tests for LoadConversationHistoryAsync mapping via SelectConversationAsync.
/// Uses a fake HttpMessageHandler to return canned history responses.
/// </summary>
public sealed class AgentSessionManagerHistoryMappingTests : IDisposable
{
    private readonly FakeHttpHandler _handler;
    private readonly AgentSessionManager _manager;

    public AgentSessionManagerHistoryMappingTests()
    {
        _handler = new FakeHttpHandler();
        var http = new HttpClient(_handler) { BaseAddress = new Uri("http://test") };
        var hub = new GatewayHubConnection();
        var js = NSubstitute.Substitute.For<Microsoft.JSInterop.IJSRuntime>();
        var historyCache = new ConversationHistoryCache(js);
        var featureFlags = new FeatureFlagsService(js);
        _manager = new AgentSessionManager(hub, http, historyCache, featureFlags);
        // Set _apiBaseUrl via reflection since InitializeAsync would require a real hub
        var field = typeof(AgentSessionManager).GetField("_apiBaseUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(_manager, "http://test/api/");
    }

    public void Dispose() => _manager.Dispose();

    private void SetupAgentWithConversation(string agentId, string conversationId)
    {
        // Add agent session state
        var sessions = (Dictionary<string, AgentSessionState>)typeof(AgentSessionManager)
            .GetField("_sessions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_manager)!;

        var state = new AgentSessionState { AgentId = agentId, DisplayName = agentId, IsConnected = true };
        state.Conversations[conversationId] = new ConversationListItemState
        {
            ConversationId = conversationId,
            Title = "Test",
            IsDefault = false,
            Status = "active",
            ActiveSessionId = "sess-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        sessions[agentId] = state;
    }

    private void SetupHistoryResponse(string conversationId, IReadOnlyList<ConversationHistoryEntryDto> entries)
    {
        var response = new ConversationHistoryResponseDto(conversationId, entries.Count, 0, 200, entries);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _handler.Responses[$"/api/conversations/{Uri.EscapeDataString(conversationId)}/history?limit=200"] = json;
    }

    [Fact]
    public async Task SelectConversation_ToolEntry_MapsIsToolCallAndToolResult()
    {
        const string agentId = "agent-1";
        const string convId = "conv-1";
        SetupAgentWithConversation(agentId, convId);
        SetupHistoryResponse(convId,
        [
            new ConversationHistoryEntryDto
            {
                Kind = "message",
                SessionId = "sess-1",
                Timestamp = DateTimeOffset.UtcNow,
                Role = "tool",
                Content = "search result",
                ToolName = "search",
                ToolCallId = "tc1",
                ToolArgs = "{\"q\":\"test\"}",
                ToolIsError = true
            }
        ]);

        await _manager.SelectConversationAsync(agentId, convId);

        var state = _manager.Sessions[agentId];
        var msg = state.Messages.ShouldHaveSingleItem();
        msg.IsToolCall.ShouldBeTrue();
        msg.ToolResult.ShouldBe("search result");
        msg.ToolArgs.ShouldBe("{\"q\":\"test\"}");
        msg.ToolIsError.ShouldBeTrue();
    }

    [Fact]
    public async Task SelectConversation_ToolEntry_WithToolIsErrorFalse_ToolIsErrorIsFalse()
    {
        const string agentId = "agent-2";
        const string convId = "conv-2";
        SetupAgentWithConversation(agentId, convId);
        SetupHistoryResponse(convId,
        [
            new ConversationHistoryEntryDto
            {
                Kind = "message",
                SessionId = "sess-1",
                Timestamp = DateTimeOffset.UtcNow,
                Role = "tool",
                Content = "ok",
                ToolName = "run",
                ToolCallId = "tc2",
                ToolIsError = false
            }
        ]);

        await _manager.SelectConversationAsync(agentId, convId);

        var msg = _manager.Sessions[agentId].Messages.ShouldHaveSingleItem();
        msg.IsToolCall.ShouldBeTrue();
        msg.ToolIsError.ShouldBeFalse();
    }

    [Fact]
    public async Task SelectConversation_NonToolEntry_IsToolCallFalse_ToolResultNull()
    {
        const string agentId = "agent-3";
        const string convId = "conv-3";
        SetupAgentWithConversation(agentId, convId);
        SetupHistoryResponse(convId,
        [
            new ConversationHistoryEntryDto
            {
                Kind = "message",
                SessionId = "sess-1",
                Timestamp = DateTimeOffset.UtcNow,
                Role = "user",
                Content = "hello",
                ToolName = null
            }
        ]);

        await _manager.SelectConversationAsync(agentId, convId);

        var msg = _manager.Sessions[agentId].Messages.ShouldHaveSingleItem();
        msg.IsToolCall.ShouldBeFalse();
        msg.ToolResult.ShouldBeNull();
    }

    [Fact]
    public async Task SelectConversation_BoundaryEntry_IsBoundaryTrueNotToolCall()
    {
        const string agentId = "agent-4";
        const string convId = "conv-4";
        SetupAgentWithConversation(agentId, convId);
        SetupHistoryResponse(convId,
        [
            new ConversationHistoryEntryDto
            {
                Kind = "boundary",
                SessionId = "sess-old",
                Timestamp = DateTimeOffset.UtcNow
            }
        ]);

        await _manager.SelectConversationAsync(agentId, convId);

        var msg = _manager.Sessions[agentId].Messages.ShouldHaveSingleItem();
        msg.Kind.ShouldBe("boundary");
        msg.IsToolCall.ShouldBeFalse();
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        public Dictionary<string, string> Responses { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            if (Responses.TryGetValue(path, out var json))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
