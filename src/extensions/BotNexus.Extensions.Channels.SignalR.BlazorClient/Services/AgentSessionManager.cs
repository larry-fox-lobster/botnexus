using System.Net.Http.Json;
using System.Text.Json;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

/// <summary>
/// Manages per-agent session state and routes SignalR hub events to the correct agent.
/// No global "current session" — each chat panel handles its own state independently.
/// Multiple agents can stream simultaneously; each buffers independently.
/// </summary>
public sealed class AgentSessionManager : IDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    private readonly GatewayHubConnection _hub;
    private readonly HttpClient _http;
    private readonly Dictionary<string, AgentSessionState> _sessions = new();
    private readonly Dictionary<string, string> _sessionToAgent = new(); // sessionId → agentId
    private readonly HashSet<string> _streamingWhenDisconnected = new();

    private string? _apiBaseUrl;

    /// <summary>Raised when any agent's state changes. UI components use this to trigger re-render.</summary>
    public event Action? OnStateChanged;

    /// <summary>All per-agent session states, keyed by agent ID.</summary>
    public IReadOnlyDictionary<string, AgentSessionState> Sessions => _sessions;

    /// <summary>The underlying hub connection for status checks.</summary>
    public GatewayHubConnection Hub => _hub;

    /// <summary>The currently active/visible agent tab. Used for unread tracking.</summary>
    public string? ActiveAgentId { get; private set; }

    /// <summary>The base URL for REST API calls.</summary>
    public string? ApiBaseUrl => _apiBaseUrl;

    public AgentSessionManager(GatewayHubConnection hub, HttpClient http)
    {
        _hub = hub;
        _http = http;
        _hub.OnConnected += HandleConnected;
        _hub.OnMessageStart += HandleMessageStart;
        _hub.OnContentDelta += HandleContentDelta;
        _hub.OnThinkingDelta += HandleThinkingDelta;
        _hub.OnToolStart += HandleToolStart;
        _hub.OnToolEnd += HandleToolEnd;
        _hub.OnMessageEnd += HandleMessageEnd;
        _hub.OnError += HandleError;
        _hub.OnSessionReset += HandleSessionReset;
        _hub.OnReconnecting += HandleReconnecting;
        _hub.OnReconnected += HandleReconnected;
        _hub.OnDisconnected += HandleDisconnected;
        _hub.OnSubAgentSpawned += HandleSubAgentSpawned;
        _hub.OnSubAgentCompleted += HandleSubAgentCompleted;
        _hub.OnSubAgentFailed += HandleSubAgentFailed;
        _hub.OnSubAgentKilled += HandleSubAgentKilled;
    }

    /// <summary>
    /// Connects to the hub and subscribes to all active sessions.
    /// Creates an <see cref="AgentSessionState"/> for each agent reported by the server.
    /// </summary>
    public async Task InitializeAsync(string hubUrl)
    {
        _apiBaseUrl = new Uri(new Uri(hubUrl), "/api/").ToString();
        await _hub.ConnectAsync(hubUrl);

        // SubscribeAll returns existing sessions — map them to agents
        var result = await _hub.SubscribeAllAsync();
        foreach (var session in result.Sessions)
        {
            RegisterSession(session.AgentId, session.SessionId, session.ChannelType);
        }
    }

    /// <summary>Set the active agent tab, clear its unread count, and load conversations if needed.</summary>
    public async Task SetActiveAgentAsync(string? agentId)
    {
        ActiveAgentId = agentId;
        if (agentId is not null && _sessions.TryGetValue(agentId, out var state))
        {
            state.UnreadCount = 0;

            // Load conversations on first visit
            if (!state.ConversationsLoaded && !state.IsLoadingConversations)
            {
                await LoadConversationsAsync(agentId);
            }
            else if (!state.ConversationsLoaded && state.IsLoadingConversations)
            {
                // Loading already in progress (from HandleConnected retry)
                // Poll briefly — it should complete within a few hundred ms
                for (var i = 0; i < 20 && !state.ConversationsLoaded; i++)
                    await Task.Delay(100);

                // Select default conversation if loaded
                if (state.ConversationsLoaded && state.ActiveConversationId is null && state.Conversations.Count > 0)
                {
                    var defaultConv = state.Conversations.Values.FirstOrDefault(c => c.IsDefault)
                        ?? state.Conversations.Values.OrderByDescending(c => c.UpdatedAt).First();
                    await SelectConversationAsync(agentId, defaultConv.ConversationId);
                }
            }
            else if (state.ActiveConversationId is not null
                && !state.ConversationHistoryLoaded.Contains(state.ActiveConversationId))
            {
                await LoadConversationHistoryAsync(agentId, state.ActiveConversationId);
            }
        }

        OnStateChanged?.Invoke();
    }

    /// <summary>Send a message to the specified agent, creating a session if needed.</summary>
    public async Task SendMessageAsync(string agentId, string content)
    {
        if (!_sessions.TryGetValue(agentId, out var state))
            return;

        // Ensure we have an active conversation before sending — creates a default one if absent.
        // This also guarantees convId is non-null in all downstream event handlers,
        // preventing the IsStreaming-stuck and message-loss bugs.
        if (state.ActiveConversationId is null)
        {
            var convId = await CreateConversationAsync(agentId, title: null, select: true);
            if (convId is null)
            {
                // Couldn't create conversation — surface error and bail
                GetOrCreateMessageStore(state, state.ActiveConversationId)
                    .Add(new ChatMessage("Error", "Failed to create conversation before sending.", DateTimeOffset.UtcNow));
                OnStateChanged?.Invoke();
                return;
            }
        }

        // Route user message via the conversation store (never into the temp []).
        GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("User", content, DateTimeOffset.UtcNow));
        OnStateChanged?.Invoke();

        try
        {
            var result = await _hub.SendMessageAsync(agentId, state.ChannelType ?? "signalr", content, state.ActiveConversationId);
            RegisterSession(agentId, result.SessionId, result.ChannelType);

            // Refresh conversation list so the server-assigned ActiveSessionId is picked up.
            // This lets FindConversationIdForSession resolve correctly for subsequent events.
            await RefreshConversationsAsync(agentId);
        }
        catch (Exception ex)
        {
            GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("Error", $"Send failed: {ex.Message}", DateTimeOffset.UtcNow));
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>Steer an in-progress agent response.</summary>
    public async Task SteerAsync(string agentId, string content)
    {
        if (!_sessions.TryGetValue(agentId, out var state) || state.SessionId is null)
            return;

        GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("User", $"🔀 {content}", DateTimeOffset.UtcNow));
        OnStateChanged?.Invoke();

        try
        {
            await _hub.SteerAsync(agentId, state.SessionId, content);
        }
        catch (Exception ex)
        {
            GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("Error", $"Steer failed: {ex.Message}", DateTimeOffset.UtcNow));
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>Send a follow-up message into an existing session.</summary>
    public async Task FollowUpAsync(string agentId, string content)
    {
        if (!_sessions.TryGetValue(agentId, out var state) || state.SessionId is null)
            return;

        GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("User", content, DateTimeOffset.UtcNow));
        OnStateChanged?.Invoke();

        try
        {
            await _hub.FollowUpAsync(agentId, state.SessionId, content);
        }
        catch (Exception ex)
        {
            GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("Error", $"Follow-up failed: {ex.Message}", DateTimeOffset.UtcNow));
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>Abort an in-progress agent response.</summary>
    public async Task AbortAsync(string agentId)
    {
        if (!_sessions.TryGetValue(agentId, out var state) || state.SessionId is null)
            return;

        try
        {
            await _hub.AbortAsync(agentId, state.SessionId);
        }
        catch (Exception ex)
        {
            GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("Error", $"Abort failed: {ex.Message}", DateTimeOffset.UtcNow));
            OnStateChanged?.Invoke();
        }
    }
    public async Task ResetSessionAsync(string agentId)
    {
        if (!_sessions.TryGetValue(agentId, out var state) || state.SessionId is null)
            return;

        try
        {
            await _hub.ResetSessionAsync(agentId, state.SessionId);
            // The server will send a SessionReset event that HandleSessionReset processes.
        }
        catch (Exception ex)
        {
            GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("Error", $"Reset failed: {ex.Message}", DateTimeOffset.UtcNow));
            OnStateChanged?.Invoke();
        }
    }
    public async Task<CompactSessionResult?> CompactSessionAsync(string agentId)
    {
        if (!_sessions.TryGetValue(agentId, out var state) || state.SessionId is null)
            return null;

        try
        {
            var result = await _hub.CompactSessionAsync(agentId, state.SessionId);
            GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("System",
                $"Session compacted: {result.Summarized} messages summarized, {result.Preserved} preserved. " +
                $"Tokens: {result.TokensBefore} → {result.TokensAfter}",
                DateTimeOffset.UtcNow));
            OnStateChanged?.Invoke();
            return result;
        }
        catch (Exception ex)
        {
            GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("Error", $"Compact failed: {ex.Message}", DateTimeOffset.UtcNow));
            OnStateChanged?.Invoke();
            return null;
        }
    }

    /// <summary>Clear local messages without resetting the server session.</summary>
    public void ClearLocalMessages(string agentId)
    {
        if (_sessions.TryGetValue(agentId, out var state))
        {
            var store = GetOrCreateMessageStore(state, state.ActiveConversationId);
            store.Clear();
            store.Add(new ChatMessage("System", "Local messages cleared.", DateTimeOffset.UtcNow));
            // Allow history to be re-fetched for this conversation
            if (state.ActiveConversationId is not null)
                state.ConversationHistoryLoaded.Remove(state.ActiveConversationId);
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>Load message history from the REST API for the given agent.
    /// Queries all sessions for the agent (not channel-scoped) and loads the most recent one.</summary>
    public async Task LoadHistoryAsync(string agentId)
    {
        if (!_sessions.TryGetValue(agentId, out var state))
            return;
        if (state.HistoryLoaded || state.IsLoadingHistory)
            return;

        state.IsLoadingHistory = true;
        OnStateChanged?.Invoke();

        try
        {
            // Get all sessions for this agent across all channels
            var sessionsUrl = $"{_apiBaseUrl}sessions?agentId={Uri.EscapeDataString(agentId)}";
            var allSessions = await _http.GetFromJsonAsync<List<SessionSummary>>(sessionsUrl);

            // Find the most recent interactive session
            var recentSession = allSessions
                ?.Where(s => s.IsInteractive && s.Status != "Sealed")
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .FirstOrDefault();

            if (recentSession is not null)
            {
                // Resume into this session so new messages go to the same session
                if (state.SessionId is null)
                    state.SessionId = recentSession.SessionId;

                // Load its history
                var historyUrl = $"{_apiBaseUrl}sessions/{Uri.EscapeDataString(recentSession.SessionId)}/history?limit=50";
                var historyResponse = await _http.GetFromJsonAsync<SessionHistoryResponse>(historyUrl);

                if (historyResponse?.Messages is { Count: > 0 })
                {
                    state.Messages.Clear();
                    foreach (var msg in historyResponse.Messages)
                    {
                        state.Messages.Add(new ChatMessage(
                            MapRole(msg.Role),
                            msg.Content,
                            msg.Timestamp)
                        {
                            ToolName = msg.ToolName,
                            ToolCallId = msg.ToolCallId,
                            IsToolCall = msg.ToolName is not null
                        });
                    }
                }
            }

            state.HistoryLoaded = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load history for {agentId}: {ex.Message}");
            state.HistoryLoaded = true; // Don't retry on failure
        }
        finally
        {
            state.IsLoadingHistory = false;
            OnStateChanged?.Invoke();
        }
    }

    // ── Conversation methods ─────────────────────────────────────────────────

    /// <summary>Load the conversation list for the given agent from the REST API.</summary>
    public async Task LoadConversationsAsync(string agentId)
    {
        if (!_sessions.TryGetValue(agentId, out var state))
            return;
        if (state.ConversationsLoaded || state.IsLoadingConversations)
            return;

        // If API URL not yet available, return early without marking loaded — allows retry
        if (_apiBaseUrl is null)
            return;

        state.IsLoadingConversations = true;
        OnStateChanged?.Invoke();

        try
        {
            var url = $"{_apiBaseUrl}conversations?agentId={Uri.EscapeDataString(agentId)}";
            var list = await _http.GetFromJsonAsync<List<ConversationSummaryDto>>(url);

            if (list is not null)
            {
                state.Conversations.Clear();
                foreach (var dto in list)
                {
                    state.Conversations[dto.ConversationId] = new ConversationListItemState
                    {
                        ConversationId = dto.ConversationId,
                        Title = dto.Title,
                        IsDefault = dto.IsDefault,
                        Status = dto.Status,
                        ActiveSessionId = dto.ActiveSessionId,
                        CreatedAt = dto.CreatedAt,
                        UpdatedAt = dto.UpdatedAt
                    };
                }
            }

            // Auto-select a conversation if none is selected
            if (state.ActiveConversationId is null && state.Conversations.Count > 0)
            {
                var defaultConv = state.Conversations.Values.FirstOrDefault(c => c.IsDefault)
                    ?? state.Conversations.Values.OrderByDescending(c => c.UpdatedAt).First();
                state.ActiveConversationId = defaultConv.ConversationId;

                // Sync SessionId to the active conversation's session
                if (defaultConv.ActiveSessionId is not null)
                    state.SessionId = defaultConv.ActiveSessionId;
            }

            state.ConversationsLoaded = true;

            // Pre-create message stores for all known conversations (Change 6)
            foreach (var conv in state.Conversations.Values)
                state.ConversationMessageStores.TryAdd(conv.ConversationId, new List<ChatMessage>());

            // If this is the currently visible agent, load history now
            if (agentId == ActiveAgentId && state.ActiveConversationId is not null)
            {
                var conv = state.Conversations.GetValueOrDefault(state.ActiveConversationId);
                if (conv is not null && !conv.HistoryLoaded && !conv.IsLoadingHistory)
                    await LoadConversationHistoryAsync(agentId, state.ActiveConversationId);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load conversations for {agentId}: {ex.Message}");
            state.ConversationsLoaded = true; // Don't retry on failure
        }
        finally
        {
            state.IsLoadingConversations = false;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>Select a conversation for the given agent and load its history if needed.</summary>
    public async Task SelectConversationAsync(string agentId, string conversationId)
    {
        if (!_sessions.TryGetValue(agentId, out var state))
            return;
        if (!state.Conversations.TryGetValue(conversationId, out var conv))
            return;

        state.ActiveConversationId = conversationId;

        // Sync active session to the selected conversation's live session.
        // If the conversation has no session yet (brand new), clear the stale previous session ID
        // so the UI doesn't show the wrong session until the first message is sent.
        state.SessionId = conv.ActiveSessionId;

        // Clear conversation unread count
        conv.UnreadCount = 0;

        // Lazy-load history ONLY if we have never loaded this conversation before.
        // Do NOT clear messages — if they're already there (from streaming) keep them.
        if (!state.ConversationHistoryLoaded.Contains(conversationId))
            await LoadConversationHistoryAsync(agentId, conversationId);

        OnStateChanged?.Invoke();
    }

    /// <summary>Create a new conversation for the given agent.</summary>
    public async Task<string?> CreateConversationAsync(string agentId, string? title = null, bool select = true)
    {
        if (!_sessions.TryGetValue(agentId, out var state))
            return null;

        try
        {
            var request = new CreateConversationRequestDto(agentId, title);
            var response = await _http.PostAsJsonAsync($"{_apiBaseUrl}conversations", request);
            response.EnsureSuccessStatusCode();

            var dto = await response.Content.ReadFromJsonAsync<ConversationResponseDto>();
            if (dto is null)
                return null;

            state.Conversations[dto.ConversationId] = new ConversationListItemState
            {
                ConversationId = dto.ConversationId,
                Title = dto.Title,
                IsDefault = dto.IsDefault,
                Status = dto.Status,
                ActiveSessionId = dto.ActiveSessionId,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt
            };

            // Initialise an empty store for the new conversation (Change 4)
            state.ConversationMessageStores[dto.ConversationId] = new List<ChatMessage>();
            state.ConversationHistoryLoaded.Add(dto.ConversationId); // brand new — nothing to load

            if (select)
            {
                await SelectConversationAsync(agentId, dto.ConversationId);
            }
            else
                OnStateChanged?.Invoke();

            return dto.ConversationId;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to create conversation for {agentId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Refresh the agent list from the REST API, adding any new agents.</summary>
    public async Task RefreshAgentsAsync()
    {
        if (_apiBaseUrl is null)
            return;

        try
        {
            var url = $"{_apiBaseUrl}agents";
            var agents = await _http.GetFromJsonAsync<List<AgentSummary>>(url);
            if (agents is null)
                return;

            foreach (var agent in agents)
            {
                if (!_sessions.ContainsKey(agent.AgentId))
                {
                    _sessions[agent.AgentId] = new AgentSessionState
                    {
                        AgentId = agent.AgentId,
                        DisplayName = agent.DisplayName,
                        IsConnected = true
                    };
                }
                else
                {
                    _sessions[agent.AgentId].DisplayName = agent.DisplayName;
                }
            }

            OnStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to refresh agents: {ex.Message}");
        }
    }

    /// <summary>Rename a conversation via PATCH /api/conversations/{id}.</summary>
    public async Task RenameConversationAsync(string agentId, string? conversationId, string newTitle)
    {
        if (conversationId is null || !_sessions.TryGetValue(agentId, out var state))
            return;
        if (!state.Conversations.TryGetValue(conversationId, out var conv))
            return;
        if (string.IsNullOrWhiteSpace(newTitle))
            return;

        try
        {
            var url = $"{_apiBaseUrl}conversations/{Uri.EscapeDataString(conversationId)}";
            var request = new PatchConversationRequestDto(newTitle);
            var response = await _http.PatchAsJsonAsync(url, request);
            response.EnsureSuccessStatusCode();

            conv.Title = newTitle;
            OnStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to rename conversation {conversationId}: {ex.Message}");
        }
    }
    public async Task RefreshConversationsAsync(string agentId)
    {
        if (!_sessions.TryGetValue(agentId, out var state))
            return;

        try
        {
            var url = $"{_apiBaseUrl}conversations?agentId={Uri.EscapeDataString(agentId)}";
            var list = await _http.GetFromJsonAsync<List<ConversationSummaryDto>>(url);

            if (list is null)
                return;

            var incoming = list.ToDictionary(d => d.ConversationId);

            // Add or update, preserving local UI state for existing entries
            foreach (var dto in list)
            {
                if (state.Conversations.TryGetValue(dto.ConversationId, out var existing))
                {
                    // Preserve local-only fields
                    existing.Title = dto.Title;
                    existing.IsDefault = dto.IsDefault;
                    existing.Status = dto.Status;
                    existing.ActiveSessionId = dto.ActiveSessionId;
                    existing.CreatedAt = dto.CreatedAt;
                    existing.UpdatedAt = dto.UpdatedAt;
                }
                else
                {
                    state.Conversations[dto.ConversationId] = new ConversationListItemState
                    {
                        ConversationId = dto.ConversationId,
                        Title = dto.Title,
                        IsDefault = dto.IsDefault,
                        Status = dto.Status,
                        ActiveSessionId = dto.ActiveSessionId,
                        CreatedAt = dto.CreatedAt,
                        UpdatedAt = dto.UpdatedAt
                    };
                }
            }

            // Remove any conversations no longer in the list
            var toRemove = state.Conversations.Keys.Where(id => !incoming.ContainsKey(id)).ToList();
            foreach (var id in toRemove)
                state.Conversations.Remove(id);

            // If active conversation was removed, fall back
            if (state.ActiveConversationId is not null && !state.Conversations.ContainsKey(state.ActiveConversationId))
            {
                var fallback = state.Conversations.Values.FirstOrDefault(c => c.IsDefault)
                    ?? state.Conversations.Values.OrderByDescending(c => c.UpdatedAt).FirstOrDefault();
                state.ActiveConversationId = fallback?.ConversationId;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to refresh conversations for {agentId}: {ex.Message}");
        }

        OnStateChanged?.Invoke();
    }

    /// <summary>Mark all messages in a conversation as read.</summary>
    public void MarkConversationRead(string agentId, string conversationId)
    {
        if (!_sessions.TryGetValue(agentId, out var state))
            return;
        if (!state.Conversations.TryGetValue(conversationId, out var conv))
            return;

        conv.UnreadCount = 0;

        // If this is the active agent, recompute agent-level unread as sum of conversations
        if (agentId == ActiveAgentId)
            state.UnreadCount = state.Conversations.Values.Sum(c => c.UnreadCount);

        OnStateChanged?.Invoke();
    }

    /// <summary>Load conversation history from the REST API and populate the message timeline.</summary>
    private async Task LoadConversationHistoryAsync(string agentId, string conversationId)
    {
        if (!_sessions.TryGetValue(agentId, out var state))
            return;
        if (!state.Conversations.TryGetValue(conversationId, out var conv))
            return;
        if (conv.IsLoadingHistory)
            return;

        // Get or create the store for this conversation
        if (!state.ConversationMessageStores.TryGetValue(conversationId, out var messages))
        {
            messages = new List<ChatMessage>();
            state.ConversationMessageStores[conversationId] = messages;
        }

        // Only load if empty — never overwrite existing messages (streaming may have already populated it)
        if (messages.Count > 0)
        {
            state.ConversationHistoryLoaded.Add(conversationId);
            return;
        }

        conv.IsLoadingHistory = true;
        OnStateChanged?.Invoke();

        try
        {
            var url = $"{_apiBaseUrl}conversations/{Uri.EscapeDataString(conversationId)}/history?limit=200";
            var response = await _http.GetFromJsonAsync<ConversationHistoryResponseDto>(url);

            if (response?.Entries is { Count: > 0 })
            {
                foreach (var entry in response.Entries)
                {
                    if (entry.Kind == "boundary")
                    {
                        var label = $"Session \u00b7 {entry.Timestamp.ToLocalTime():MMM d HH:mm} \u00b7 {entry.SessionId}";
                        messages.Add(new ChatMessage("System", string.Empty, entry.Timestamp)
                        {
                            Kind = "boundary",
                            BoundaryLabel = label,
                            BoundarySessionId = entry.SessionId
                        });
                    }
                    else
                    {
                        var isToolCall = entry.ToolName is not null;
                        messages.Add(new ChatMessage(
                            MapRole(entry.Role ?? "system"),
                            entry.Content ?? string.Empty,
                            entry.Timestamp)
                        {
                            ToolName = entry.ToolName,
                            ToolCallId = entry.ToolCallId,
                            IsToolCall = isToolCall,
                            // When restoring tool history, Content IS the result text.
                            // Set ToolResult so ChatPanel renders it correctly (not as pending hourglass).
                            ToolResult = isToolCall ? entry.Content : null,
                            ToolArgs = entry.ToolArgs,
                            ToolIsError = entry.ToolIsError
                        });
                    }
                }
            }

            state.ConversationHistoryLoaded.Add(conversationId);

            // Sync SessionId from the conversation after loading
            if (state.ActiveConversationId == conversationId && conv.ActiveSessionId is not null)
                state.SessionId = conv.ActiveSessionId;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load history for conversation {conversationId}: {ex.Message}");
            state.ConversationHistoryLoaded.Add(conversationId); // Don't retry on failure
        }
        finally
        {
            conv.IsLoadingHistory = false;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// View a sub-agent session in read-only mode. Creates or activates the sub-agent's session,
    /// loads its message history, and switches to the sub-agent's chat panel.
    /// </summary>
    public async Task ViewSubAgentAsync(SubAgentInfo subAgent)
    {
        var subAgentId = subAgent.SubAgentId;

        // Register or reuse the sub-agent's session state
        if (!_sessions.TryGetValue(subAgentId, out var state))
        {
            state = new AgentSessionState
            {
                AgentId = subAgentId,
                DisplayName = subAgent.Name ?? $"Sub-agent {subAgentId[..Math.Min(8, subAgentId.Length)]}",
                SessionId = subAgentId, // For sub-agents, session ID == sub-agent ID
                SessionType = "agent-subagent",
                IsConnected = true
            };
            _sessions[subAgentId] = state;
            _sessionToAgent[subAgentId] = subAgentId;
        }

        // Switch to the sub-agent's panel
        await SetActiveAgentAsync(subAgentId);

        // Load history if not already loaded
        if (!state.HistoryLoaded && !state.IsLoadingHistory && state.Messages.Count == 0)
        {
            await LoadSubAgentHistoryAsync(subAgentId);
        }
    }

    /// <summary>Load message history for a sub-agent session from the REST API.</summary>
    private async Task LoadSubAgentHistoryAsync(string subAgentId)
    {
        if (!_sessions.TryGetValue(subAgentId, out var state))
            return;
        if (state.HistoryLoaded || state.IsLoadingHistory)
            return;

        state.IsLoadingHistory = true;
        OnStateChanged?.Invoke();

        try
        {
            // Sub-agent sessions use the session ID directly (not agent ID)
            var url = $"{_apiBaseUrl}sessions/{subAgentId}/history?limit=50";
            var response = await _http.GetFromJsonAsync<HistoryResponse>(url);

            if (response?.Messages is { Count: > 0 })
            {
                state.Messages.Clear();
                foreach (var msg in response.Messages)
                {
                    state.Messages.Add(new ChatMessage(
                        MapRole(msg.Role),
                        msg.Content,
                        msg.Timestamp)
                    {
                        ToolName = msg.ToolName,
                        ToolCallId = msg.ToolCallId,
                        IsToolCall = msg.ToolName is not null
                    });
                }
            }

            state.HistoryLoaded = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load sub-agent history for {subAgentId}: {ex.Message}");
            state.HistoryLoaded = true; // Don't retry on failure
        }
        finally
        {
            state.IsLoadingHistory = false;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>Register a session ID → agent ID mapping.</summary>
    public void RegisterSession(string agentId, string sessionId, string? channelType = null, string? sessionType = null)
    {
        _sessionToAgent[sessionId] = agentId;
        if (_sessions.TryGetValue(agentId, out var state))
        {
            state.SessionId = sessionId;
            if (channelType is not null)
                state.ChannelType = channelType;
            if (sessionType is not null)
                state.SessionType = sessionType;
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

        // Retry loading conversations for ALL sessions — on first page load ActiveAgentId
        // may not be set yet when HandleConnected fires, so iterate all sessions and
        // pre-load conversations for any that haven't been loaded yet.
        if (_apiBaseUrl is not null)
        {
            foreach (var agentId in _sessions.Keys.ToList())
            {
                var state = _sessions[agentId];
                if (!state.ConversationsLoaded && !state.IsLoadingConversations)
                {
                    var id = agentId;
                    _ = Task.Run(() => LoadConversationsAsync(id));
                }
            }
        }

        OnStateChanged?.Invoke();
    }

    private void HandleMessageStart(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        state.IsStreaming = true;
        state.CurrentStreamBuffer = "";
        state.ThinkingBuffer = "";
        state.ProcessingStage = "🤖 Agent is responding…";
        OnStateChanged?.Invoke();
    }

    private void HandleContentDelta(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        state.CurrentStreamBuffer += evt.ContentDelta ?? "";
        state.ProcessingStage = "🤖 Agent is responding…";
        OnStateChanged?.Invoke();
    }

    private void HandleThinkingDelta(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        state.ThinkingBuffer += evt.ThinkingContent ?? "";
        state.ProcessingStage = "💭 Thinking…";
        OnStateChanged?.Invoke();
    }

    private void HandleToolStart(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        var convId = FindConversationIdForSession(state, evt.SessionId) ?? state.ActiveConversationId;
        if (convId is null) return;
        var msgs = GetOrCreateMessageStore(state, convId);

        var toolCallId = evt.ToolCallId ?? Guid.NewGuid().ToString("N");
        var argsJson = evt.ToolArgs is not null
            ? JsonSerializer.Serialize(evt.ToolArgs, s_jsonOptions)
            : null;

        var msg = new ChatMessage("Tool", $"⏳ Calling {evt.ToolName}…", DateTimeOffset.UtcNow)
        {
            ToolName = evt.ToolName,
            ToolCallId = toolCallId,
            ToolArgs = argsJson,
            IsToolCall = true
        };

        msgs.Add(msg);
        state.ActiveToolCalls[toolCallId] = new ActiveToolCall
        {
            ToolCallId = toolCallId,
            ToolName = evt.ToolName ?? "unknown",
            StartedAt = DateTimeOffset.UtcNow,
            MessageId = msg.Id
        };

        if (convId != state.ActiveConversationId && state.Conversations.TryGetValue(convId, out var inactiveConv))
            inactiveConv.UnreadCount++;

        state.ProcessingStage = $"🔧 Using tool: {evt.ToolName}";
        OnStateChanged?.Invoke();
    }

    private void HandleToolEnd(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        var convId = FindConversationIdForSession(state, evt.SessionId) ?? state.ActiveConversationId;
        if (convId is null) return;
        var msgs = GetOrCreateMessageStore(state, convId);

        var toolCallId = evt.ToolCallId;
        TimeSpan? duration = null;
        string? messageId = null;

        if (toolCallId is not null && state.ActiveToolCalls.TryGetValue(toolCallId, out var activeTool))
        {
            duration = DateTimeOffset.UtcNow - activeTool.StartedAt;
            messageId = activeTool.MessageId;
            state.ActiveToolCalls.Remove(toolCallId);
        }

        // Try to update the ToolStart message in-place with result and duration
        if (messageId is not null)
        {
            var index = msgs.FindIndex(m => m.Id == messageId);
            if (index >= 0)
            {
                var original = msgs[index];
                msgs[index] = original with
                {
                    Content = evt.ToolIsError == true
                        ? $"❌ {evt.ToolName} failed"
                        : $"✅ {evt.ToolName} completed",
                    ToolResult = evt.ToolResult,
                    ToolIsError = evt.ToolIsError,
                    ToolDuration = duration
                };

                state.ProcessingStage = state.IsStreaming ? "🤖 Agent is responding…" : null;
                OnStateChanged?.Invoke();
                return;
            }
        }

        // Fallback: add as a new message if the original was not found
        msgs.Add(new ChatMessage("Tool",
            evt.ToolIsError == true ? $"❌ {evt.ToolName} failed" : $"✅ {evt.ToolName} completed",
            DateTimeOffset.UtcNow)
        {
            ToolName = evt.ToolName,
            ToolCallId = toolCallId,
            ToolResult = evt.ToolResult,
            IsToolCall = true,
            ToolIsError = evt.ToolIsError,
            ToolDuration = duration
        });

        if (convId != state.ActiveConversationId && state.Conversations.TryGetValue(convId, out var inactiveConv))
            inactiveConv.UnreadCount++;

        state.ProcessingStage = state.IsStreaming ? "🤖 Agent is responding…" : null;
        OnStateChanged?.Invoke();
    }

    private void HandleMessageEnd(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        var convId = FindConversationIdForSession(state, evt.SessionId) ?? state.ActiveConversationId;
        if (convId is null) return;
        var msgs = GetOrCreateMessageStore(state, convId);

        // Finalize the thinking buffer + stream buffer into a single assistant message
        var thinkingContent = string.IsNullOrEmpty(state.ThinkingBuffer) ? null : state.ThinkingBuffer;

        if (!string.IsNullOrEmpty(state.CurrentStreamBuffer))
        {
            msgs.Add(new ChatMessage("Assistant", state.CurrentStreamBuffer, DateTimeOffset.UtcNow)
            {
                ThinkingContent = thinkingContent
            });
        }
        else if (thinkingContent is not null)
        {
            // Thinking only, no visible content — still attach it
            msgs.Add(new ChatMessage("Assistant", "", DateTimeOffset.UtcNow)
            {
                ThinkingContent = thinkingContent
            });
        }

        state.CurrentStreamBuffer = "";
        state.ThinkingBuffer = "";
        state.IsStreaming = false;
        state.ProcessingStage = null;

        // Track unread for non-active agents, and per-conversation for non-active conversations
        if (state.AgentId != ActiveAgentId)
            state.UnreadCount++;

        if (convId != state.ActiveConversationId && state.Conversations.TryGetValue(convId, out var activeConv))
            activeConv.UnreadCount++;

        OnStateChanged?.Invoke();
    }

    private void HandleError(AgentStreamEvent evt)
    {
        var state = FindStateBySessionId(evt.SessionId);
        if (state is null) return;

        var convId = FindConversationIdForSession(state, evt.SessionId) ?? state.ActiveConversationId;
        GetOrCreateMessageStore(state, convId).Add(new ChatMessage("Error", evt.ErrorMessage ?? "An unknown error occurred.", DateTimeOffset.UtcNow));
        state.IsStreaming = false;
        state.CurrentStreamBuffer = "";
        state.ThinkingBuffer = "";
        state.ProcessingStage = null;
        OnStateChanged?.Invoke();
    }

    private void HandleSessionReset(SessionResetPayload payload)
    {
        if (_sessions.TryGetValue(payload.AgentId, out var state))
        {
            if (state.SessionId is not null)
                _sessionToAgent.Remove(state.SessionId);

            state.SessionId = null;
            state.IsStreaming = false;
            state.CurrentStreamBuffer = "";
            state.ThinkingBuffer = "";
            state.UnreadCount = 0;
            state.HistoryLoaded = false;
            state.ActiveToolCalls.Clear();
            state.SubAgents.Clear();
            state.ProcessingStage = null;

            // Clear the active conversation's store (session was reset, history is gone)
            if (state.ActiveConversationId is not null)
            {
                state.ConversationMessageStores.TryGetValue(state.ActiveConversationId, out var store);
                store?.Clear();
                state.ConversationHistoryLoaded.Remove(state.ActiveConversationId);
                GetOrCreateMessageStore(state, state.ActiveConversationId)
                    .Add(new ChatMessage("System", "Session reset. Start a new conversation.", DateTimeOffset.UtcNow));
            }
        }

        OnStateChanged?.Invoke();
    }

    // ── Sub-agent event handlers ──────────────────────────────────────────

    private void HandleSubAgentSpawned(SubAgentEventPayload payload)
    {
        var state = FindStateBySessionId(payload.SessionId);
        if (state is null) return;

        state.SubAgents[payload.SubAgentId] = new SubAgentInfo
        {
            SubAgentId = payload.SubAgentId,
            Name = payload.Name,
            Task = payload.Task,
            Status = "Running",
            StartedAt = payload.StartedAt,
            Model = payload.Model,
            Archetype = payload.Archetype
        };

        // Register the sub-agent's session in the session-to-agent mapping
        _sessionToAgent[payload.SubAgentId] = payload.SubAgentId;

        GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("System",
            $"🔄 Sub-agent spawned: {payload.Name ?? payload.SubAgentId} — {payload.Task}",
            DateTimeOffset.UtcNow));

        OnStateChanged?.Invoke();
    }

    private void HandleSubAgentCompleted(SubAgentEventPayload payload)
    {
        var state = FindStateBySessionId(payload.SessionId);
        if (state is null) return;

        if (state.SubAgents.TryGetValue(payload.SubAgentId, out var sub))
        {
            sub.Status = "Completed";
            sub.CompletedAt = payload.CompletedAt;
            sub.ResultSummary = payload.ResultSummary;
        }

        GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("System",
            $"✅ Sub-agent completed: {payload.Name ?? payload.SubAgentId}" +
            (payload.ResultSummary is not null ? $" — {payload.ResultSummary}" : ""),
            DateTimeOffset.UtcNow));

        OnStateChanged?.Invoke();
    }

    private void HandleSubAgentFailed(SubAgentEventPayload payload)
    {
        var state = FindStateBySessionId(payload.SessionId);
        if (state is null) return;

        if (state.SubAgents.TryGetValue(payload.SubAgentId, out var sub))
        {
            sub.Status = "Failed";
            sub.CompletedAt = payload.CompletedAt;
            sub.ResultSummary = payload.ResultSummary;
        }

        GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("System",
            $"❌ Sub-agent failed: {payload.Name ?? payload.SubAgentId}" +
            (payload.ResultSummary is not null ? $" — {payload.ResultSummary}" : ""),
            DateTimeOffset.UtcNow));

        OnStateChanged?.Invoke();
    }

    private void HandleSubAgentKilled(SubAgentEventPayload payload)
    {
        var state = FindStateBySessionId(payload.SessionId);
        if (state is null) return;

        if (state.SubAgents.TryGetValue(payload.SubAgentId, out var sub))
        {
            sub.Status = "Killed";
            sub.CompletedAt = payload.CompletedAt;
        }

        GetOrCreateMessageStore(state, state.ActiveConversationId).Add(new ChatMessage("System",
            $"⛔ Sub-agent killed: {payload.Name ?? payload.SubAgentId}",
            DateTimeOffset.UtcNow));

        OnStateChanged?.Invoke();
    }

    // ── Connection lifecycle ──────────────────────────────────────────────

    private void HandleReconnecting()
    {
        foreach (var state in _sessions.Values)
        {
            if (state.IsStreaming)
                _streamingWhenDisconnected.Add(state.AgentId);
            state.IsConnected = false;
        }

        OnStateChanged?.Invoke();
    }

    private async void HandleReconnected()
    {
        // Mark all agents as connected again
        foreach (var state in _sessions.Values)
            state.IsConnected = true;

        try
        {
            // Re-subscribe to all session groups after reconnection
            var result = await _hub.SubscribeAllAsync();
            foreach (var session in result.Sessions)
                RegisterSession(session.AgentId, session.SessionId, session.ChannelType);

            // Recover any agents that were streaming when disconnect occurred
            foreach (var agentId in _streamingWhenDisconnected)
            {
                if (_sessions.TryGetValue(agentId, out var state))
                {
                    state.IsStreaming = false;
                    state.CurrentStreamBuffer = "";
                    state.ThinkingBuffer = "";
                    state.ProcessingStage = null;
                    state.ConversationsLoaded = false; // Force reload to pick up missed messages
                    await LoadConversationsAsync(agentId);
                }
            }

            _streamingWhenDisconnected.Clear();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Reconnect recovery failed: {ex.Message}");
        }

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

    /// <summary>Find the conversation ID whose active session matches the given session ID.</summary>
    private static string? FindConversationIdForSession(AgentSessionState state, string? sessionId)
    {
        if (sessionId is null) return null;
        return state.Conversations.Values
            .FirstOrDefault(c => c.ActiveSessionId == sessionId)
            ?.ConversationId;
    }

    /// <summary>Get or create the message store for a conversation.</summary>
    private static List<ChatMessage> GetOrCreateMessageStore(AgentSessionState state, string? convId)
    {
        if (convId is null) return [];
        if (!state.ConversationMessageStores.TryGetValue(convId, out var msgs))
        {
            msgs = new List<ChatMessage>();
            state.ConversationMessageStores[convId] = msgs;
        }
        return msgs;
    }

    private static string MapRole(string role) => role.ToLowerInvariant() switch
    {
        "user" => "User",
        "assistant" => "Assistant",
        "tool" => "Tool",
        "error" => "Error",
        "system" => "System",
        _ => role
    };

    /// <inheritdoc />
    public void Dispose()
    {
        _hub.OnConnected -= HandleConnected;
        _hub.OnMessageStart -= HandleMessageStart;
        _hub.OnContentDelta -= HandleContentDelta;
        _hub.OnThinkingDelta -= HandleThinkingDelta;
        _hub.OnToolStart -= HandleToolStart;
        _hub.OnToolEnd -= HandleToolEnd;
        _hub.OnMessageEnd -= HandleMessageEnd;
        _hub.OnError -= HandleError;
        _hub.OnSessionReset -= HandleSessionReset;
        _hub.OnReconnecting -= HandleReconnecting;
        _hub.OnReconnected -= HandleReconnected;
        _hub.OnDisconnected -= HandleDisconnected;
        _hub.OnSubAgentSpawned -= HandleSubAgentSpawned;
        _hub.OnSubAgentCompleted -= HandleSubAgentCompleted;
        _hub.OnSubAgentFailed -= HandleSubAgentFailed;
        _hub.OnSubAgentKilled -= HandleSubAgentKilled;
    }
}
