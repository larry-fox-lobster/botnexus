namespace BotNexus.Channels.SignalR.BlazorClient.Services;

/// <summary>
/// Manages per-agent session state and routes SignalR hub events to the correct agent.
/// No global "current session" — each chat panel handles its own state independently.
/// Multiple agents can stream simultaneously; each buffers independently.
/// </summary>
public sealed class AgentSessionManager : IDisposable
{
    private readonly GatewayHubConnection _hub;
    private readonly Dictionary<string, AgentSessionState> _sessions = new();
    private readonly Dictionary<string, string> _sessionToAgent = new(); // sessionId → agentId

    /// <summary>Raised when any agent's state changes. UI components use this to trigger re-render.</summary>
    public event Action? OnStateChanged;

    /// <summary>All per-agent session states, keyed by agent ID.</summary>
    public IReadOnlyDictionary<string, AgentSessionState> Sessions => _sessions;

    /// <summary>The underlying hub connection for status checks.</summary>
    public GatewayHubConnection Hub => _hub;

    /// <summary>The currently active/visible agent tab. Used for unread tracking.</summary>
    public string? ActiveAgentId { get; private set; }

    public AgentSessionManager(GatewayHubConnection hub)
    {
        _hub = hub;
        _hub.OnConnected += HandleConnected;
        _hub.OnMessageStart += HandleMessageStart;
        _hub.OnContentDelta += HandleContentDelta;
        _hub.OnToolStart += HandleToolStart;
        _hub.OnToolEnd += HandleToolEnd;
        _hub.OnMessageEnd += HandleMessageEnd;
        _hub.OnError += HandleError;
        _hub.OnSessionReset += HandleSessionReset;
        _hub.OnReconnecting += HandleReconnecting;
        _hub.OnDisconnected += HandleDisconnected;
    }

    /// <summary>
    /// Connects to the hub and subscribes to all active sessions.
    /// Creates an <see cref="AgentSessionState"/> for each agent reported by the server.
    /// </summary>
    public async Task InitializeAsync(string hubUrl)
    {
        await _hub.ConnectAsync(hubUrl);

        // SubscribeAll returns existing sessions — map them to agents
        var result = await _hub.SubscribeAllAsync();
        foreach (var session in result.Sessions)
        {
            RegisterSession(session.AgentId, session.SessionId, session.ChannelType);
        }
    }

    /// <summary>Set the active agent tab and clear its unread count.</summary>
    public void SetActiveAgent(string? agentId)
    {
        ActiveAgentId = agentId;
        if (agentId is not null && _sessions.TryGetValue(agentId, out var state))
        {
            state.UnreadCount = 0;
        }

        OnStateChanged?.Invoke();
    }

    /// <summary>Send a message to the specified agent, creating a session if needed.</summary>
    public async Task SendMessageAsync(string agentId, string content)
    {
        if (!_sessions.TryGetValue(agentId, out var state))
            return;

        state.Messages.Add(new ChatMessage("User", content, DateTimeOffset.UtcNow));
        OnStateChanged?.Invoke();

        try
        {
            var result = await _hub.SendMessageAsync(agentId, state.ChannelType ?? "signalr", content);
            RegisterSession(agentId, result.SessionId, result.ChannelType);
        }
        catch (Exception ex)
        {
            state.Messages.Add(new ChatMessage("Error", $"Send failed: {ex.Message}", DateTimeOffset.UtcNow));
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>Register a session ID → agent ID mapping.</summary>
    public void RegisterSession(string agentId, string sessionId, string? channelType = null)
    {
        _sessionToAgent[sessionId] = agentId;
        if (_sessions.TryGetValue(agentId, out var state))
        {
            state.SessionId = sessionId;
            if (channelType is not null)
                state.ChannelType = channelType;
        }
    }

    // ── Event routing ─────────────────────────────────────────────────────
    // Each handler finds the correct AgentSessionState by sessionId and updates it.
    // OnStateChanged fires to trigger UI re-render.

    private void HandleConnected(ConnectedPayload payload)
    {
        _sessions.Clear();
        _sessionToAgent.Clear();

        foreach (var agent in payload.Agents)
        {
            _sessions[agent.AgentId] = new AgentSessionState
            {
                AgentId = agent.AgentId,
                DisplayName = agent.DisplayName,
                IsConnected = true
            };
        }

        OnStateChanged?.Invoke();
    }

    private void HandleMessageStart(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        state.IsStreaming = true;
        state.CurrentStreamBuffer = "";
        OnStateChanged?.Invoke();
    }

    private void HandleContentDelta(ContentDeltaPayload payload)
    {
        var state = FindStateBySessionId(payload.SessionId);
        if (state is null) return;

        state.CurrentStreamBuffer += payload.ContentDelta ?? "";
        OnStateChanged?.Invoke();
    }

    private void HandleToolStart(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        state.Messages.Add(new ChatMessage("Tool", $"Calling {evt.ToolName}…", DateTimeOffset.UtcNow)
        {
            ToolName = evt.ToolName,
            IsToolCall = true
        });
        OnStateChanged?.Invoke();
    }

    private void HandleToolEnd(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        var display = evt.ToolIsError == true
            ? $"❌ {evt.ToolName} failed"
            : $"✅ {evt.ToolName} completed";

        state.Messages.Add(new ChatMessage("Tool", display, DateTimeOffset.UtcNow)
        {
            ToolName = evt.ToolName,
            ToolResult = evt.ToolResult,
            IsToolCall = true
        });
        OnStateChanged?.Invoke();
    }

    private void HandleMessageEnd(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        if (!string.IsNullOrEmpty(state.CurrentStreamBuffer))
        {
            state.Messages.Add(new ChatMessage("Assistant", state.CurrentStreamBuffer, DateTimeOffset.UtcNow));
        }

        state.CurrentStreamBuffer = "";
        state.IsStreaming = false;

        // Track unread for non-active agents
        if (state.AgentId != ActiveAgentId)
        {
            state.UnreadCount++;
        }

        OnStateChanged?.Invoke();
    }

    private void HandleError(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        state.Messages.Add(new ChatMessage("Error", evt.ErrorMessage ?? "An unknown error occurred.", DateTimeOffset.UtcNow));
        state.IsStreaming = false;
        state.CurrentStreamBuffer = "";
        OnStateChanged?.Invoke();
    }

    private void HandleSessionReset(SessionResetPayload payload)
    {
        if (_sessions.TryGetValue(payload.AgentId, out var state))
        {
            if (state.SessionId is not null)
                _sessionToAgent.Remove(state.SessionId);

            state.SessionId = null;
            state.Messages.Clear();
            state.IsStreaming = false;
            state.CurrentStreamBuffer = "";
            state.UnreadCount = 0;
        }

        OnStateChanged?.Invoke();
    }

    private void HandleReconnecting()
    {
        foreach (var state in _sessions.Values)
            state.IsConnected = false;
        OnStateChanged?.Invoke();
    }

    private void HandleDisconnected()
    {
        foreach (var state in _sessions.Values)
            state.IsConnected = false;
        OnStateChanged?.Invoke();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private AgentSessionState? FindStateBySessionId(string? sessionId)
    {
        if (sessionId is null) return null;
        return _sessionToAgent.TryGetValue(sessionId, out var agentId)
            && _sessions.TryGetValue(agentId, out var state)
            ? state
            : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _hub.OnConnected -= HandleConnected;
        _hub.OnMessageStart -= HandleMessageStart;
        _hub.OnContentDelta -= HandleContentDelta;
        _hub.OnToolStart -= HandleToolStart;
        _hub.OnToolEnd -= HandleToolEnd;
        _hub.OnMessageEnd -= HandleMessageEnd;
        _hub.OnError -= HandleError;
        _hub.OnSessionReset -= HandleSessionReset;
        _hub.OnReconnecting -= HandleReconnecting;
        _hub.OnDisconnected -= HandleDisconnected;
    }
}
