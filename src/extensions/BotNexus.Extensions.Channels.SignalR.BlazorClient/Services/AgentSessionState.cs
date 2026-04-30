namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

/// <summary>
/// Thin facade over <see cref="IClientStateStore"/> for a single agent.
/// Components continue to bind to this object in Wave 2 while the store becomes
/// the single backing state model.
/// </summary>
public sealed class AgentSessionState
{
    private readonly IClientStateStore _store;
    private static readonly List<ChatMessage> s_emptyMessages = [];
    private StoreConversationMap? _conversations;
    private StoreMessageStores? _messageStores;
    private StoreHistoryLoadedSet? _historyLoaded;

    public AgentSessionState()
    {
        _store = new ClientStateStore();
    }

    public AgentSessionState(IClientStateStore store, string agentId)
    {
        _store = store;
        AgentId = agentId;
    }

    /// <summary>The agent's unique identifier.</summary>
    public string AgentId { get; init; } = "";

    private AgentState EnsureData()
    {
        var existing = _store.GetAgent(AgentId);
        if (existing is not null)
            return existing;

        var created = new AgentState { AgentId = AgentId };
        _store.UpsertAgent(created);
        return created;
    }

    private AgentState? Data => _store.GetAgent(AgentId);

    /// <summary>Human-friendly display name for the agent.</summary>
    public string DisplayName
    {
        get => Data?.DisplayName ?? "";
        set => EnsureData().DisplayName = value;
    }

    /// <summary>Active session ID (set after first message exchange).</summary>
    public string? SessionId
    {
        get => Data?.SessionId;
        set => EnsureData().SessionId = value;
    }

    /// <summary>Channel type for this session.</summary>
    public string? ChannelType
    {
        get => Data?.ChannelType;
        set => EnsureData().ChannelType = value;
    }

    /// <summary>Session type — user-agent, agent-subagent, etc. Determines read-only behavior.</summary>
    public string SessionType
    {
        get => Data?.SessionType ?? "user-agent";
        set => EnsureData().SessionType = value;
    }

    /// <summary>
    /// Whether this session is read-only. True for sub-agent sessions — users can observe
    /// but cannot send messages.
    /// </summary>
    public bool IsReadOnly => Data?.IsReadOnly ?? false;

    /// <summary>Whether the agent is currently streaming a response.</summary>
    public bool IsStreaming
    {
        get => Data?.IsStreaming ?? false;
        set => EnsureData().IsStreaming = value;
    }

    /// <summary>Buffer for the in-progress streaming response.</summary>
    public string CurrentStreamBuffer
    {
        get => ActiveConversationId is not null ? _store.GetStreamState(ActiveConversationId).Buffer : "";
        set
        {
            if (ActiveConversationId is not null)
                _store.GetStreamState(ActiveConversationId).Buffer = value;
        }
    }

    /// <summary>Buffer for in-progress thinking content during streaming.</summary>
    public string ThinkingBuffer
    {
        get => ActiveConversationId is not null ? _store.GetStreamState(ActiveConversationId).ThinkingBuffer : "";
        set
        {
            if (ActiveConversationId is not null)
                _store.GetStreamState(ActiveConversationId).ThinkingBuffer = value;
        }
    }

    /// <summary>Whether the hub connection is active.</summary>
    public bool IsConnected
    {
        get => Data?.IsConnected ?? false;
        set => EnsureData().IsConnected = value;
    }

    /// <summary>Count of unread messages while this agent's tab is not active.</summary>
    public int UnreadCount
    {
        get => Data?.UnreadCount ?? 0;
        set => EnsureData().UnreadCount = value;
    }

    /// <summary>Legacy agent-level history flag kept for API compatibility in Wave 2.</summary>
    public bool HistoryLoaded { get; set; }

    /// <summary>Legacy agent-level in-flight history flag kept for API compatibility in Wave 2.</summary>
    public bool IsLoadingHistory { get; set; }

    /// <summary>In-progress tool calls keyed by tool-call ID.</summary>
    public Dictionary<string, ActiveToolCall> ActiveToolCalls => EnsureData().ActiveToolCalls;

    /// <summary>Sub-agents spawned by this agent, keyed by sub-agent ID.</summary>
    public Dictionary<string, SubAgentInfo> SubAgents => EnsureData().SubAgents;

    /// <summary>Current processing stage description for the status bar.</summary>
    public string? ProcessingStage
    {
        get => Data?.ProcessingStage;
        set => EnsureData().ProcessingStage = value;
    }

    /// <summary>Whether tool messages are visible in the chat panel.</summary>
    public bool ShowTools
    {
        get => Data?.ShowTools ?? true;
        set => EnsureData().ShowTools = value;
    }

    /// <summary>Whether thinking blocks are visible in the chat panel.</summary>
    public bool ShowThinking
    {
        get => Data?.ShowThinking ?? true;
        set => EnsureData().ShowThinking = value;
    }

    /// <summary>All conversations for this agent, keyed by conversation ID.</summary>
    public StoreConversationMap Conversations => _conversations ??= new StoreConversationMap(_store, AgentId);

    /// <summary>The currently selected conversation ID.</summary>
    public string? ActiveConversationId
    {
        get => Data?.ActiveConversationId;
        set => EnsureData().ActiveConversationId = value;
    }

    /// <summary>Whether the conversation list has been loaded from the REST API.</summary>
    public bool ConversationsLoaded
    {
        get => Data?.ConversationsLoaded ?? false;
        set => EnsureData().ConversationsLoaded = value;
    }

    /// <summary>Whether a conversation list fetch is currently in-flight.</summary>
    public bool IsLoadingConversations
    {
        get => Data?.IsLoadingConversations ?? false;
        set => EnsureData().IsLoadingConversations = value;
    }

    /// <summary>Display title of the active conversation, or null if none selected.</summary>
    public string? ActiveConversationTitle =>
        ActiveConversationId is not null && Conversations.TryGetValue(ActiveConversationId, out var c)
            ? c.Title
            : null;

    /// <summary>Messages for the currently active conversation (computed).</summary>
    public List<ChatMessage> Messages =>
        ActiveConversationId is not null && Data?.Conversations.TryGetValue(ActiveConversationId, out var conversation) == true
            ? conversation.Messages
            : s_emptyMessages;

    /// <summary>Per-conversation message stores keyed by conversation ID.</summary>
    public StoreMessageStores ConversationMessageStores => _messageStores ??= new StoreMessageStores(_store, AgentId);

    /// <summary>Set of conversation IDs whose history has been loaded from REST.</summary>
    public StoreHistoryLoadedSet ConversationHistoryLoaded => _historyLoaded ??= new StoreHistoryLoadedSet(_store, AgentId);
}

/// <summary>
/// Dictionary-like facade over the store's conversation nodes.
/// </summary>
public sealed class StoreConversationMap
{
    private readonly IClientStateStore _store;
    private readonly string _agentId;

    public StoreConversationMap(IClientStateStore store, string agentId)
    {
        _store = store;
        _agentId = agentId;
    }

    private AgentState? Agent => _store.GetAgent(_agentId);

    public ConversationListItemState this[string conversationId]
    {
        get => new ConversationListItemState(_store, _agentId, conversationId) { ConversationId = conversationId };
        set
        {
            var agent = Agent;
            if (agent is null)
                return;

            if (!agent.Conversations.TryGetValue(conversationId, out var conversation))
            {
                conversation = new ConversationState { ConversationId = conversationId };
                agent.Conversations[conversationId] = conversation;
            }

            conversation.Title = value.Title;
            conversation.IsDefault = value.IsDefault;
            conversation.Status = value.Status;
            conversation.ActiveSessionId = value.ActiveSessionId;
            conversation.UnreadCount = value.UnreadCount;
            conversation.CreatedAt = value.CreatedAt;
            conversation.UpdatedAt = value.UpdatedAt;
            conversation.HistoryLoaded = value.HistoryLoaded;
            conversation.IsLoadingHistory = value.IsLoadingHistory;
        }
    }

    public ICollection<string> Keys => Agent?.Conversations.Keys.ToList() ?? [];
    public ICollection<ConversationListItemState> Values =>
        Agent?.Conversations.Keys.Select(id => new ConversationListItemState(_store, _agentId, id) { ConversationId = id }).ToList()
        ?? [];
    public int Count => Agent?.Conversations.Count ?? 0;

    public bool TryGetValue(string conversationId, out ConversationListItemState state)
    {
        if (Agent?.Conversations.ContainsKey(conversationId) == true)
        {
            state = new ConversationListItemState(_store, _agentId, conversationId) { ConversationId = conversationId };
            return true;
        }

        state = null!;
        return false;
    }

    public ConversationListItemState? GetValueOrDefault(string? conversationId)
    {
        if (conversationId is null)
            return null;

        return TryGetValue(conversationId, out var state) ? state : null;
    }

    public void Clear() => Agent?.Conversations.Clear();

    public bool Remove(string conversationId) => Agent?.Conversations.Remove(conversationId) == true;

    public bool ContainsKey(string conversationId) => Agent?.Conversations.ContainsKey(conversationId) == true;
}

/// <summary>
/// Dict-like facade over per-conversation message lists backed by the store.
/// </summary>
public sealed class StoreMessageStores
{
    private readonly IClientStateStore _store;
    private readonly string _agentId;

    public StoreMessageStores(IClientStateStore store, string agentId)
    {
        _store = store;
        _agentId = agentId;
    }

    private AgentState? Agent => _store.GetAgent(_agentId);

    public List<ChatMessage> this[string convId]
    {
        get => GetOrCreate(convId);
        set
        {
            var list = GetOrCreate(convId);
            list.Clear();
            list.AddRange(value);
        }
    }

    public bool TryGetValue(string convId, out List<ChatMessage> messages)
    {
        if (Agent?.Conversations.TryGetValue(convId, out var conv) == true)
        {
            messages = conv.Messages;
            return true;
        }

        messages = [];
        return false;
    }

    public bool TryAdd(string convId, List<ChatMessage> messages)
    {
        var agent = Agent;
        if (agent is null)
            return false;
        if (agent.Conversations.ContainsKey(convId))
            return false;

        agent.Conversations[convId] = new ConversationState
        {
            ConversationId = convId,
            Messages = { }
        };
        agent.Conversations[convId].Messages.AddRange(messages);
        return true;
    }

    public bool ContainsKey(string convId) => Agent?.Conversations.ContainsKey(convId) == true;

    private List<ChatMessage> GetOrCreate(string convId)
    {
        var agent = Agent;
        if (agent is null)
            return [];

        if (!agent.Conversations.TryGetValue(convId, out var conv))
        {
            conv = new ConversationState { ConversationId = convId };
            agent.Conversations[convId] = conv;
        }

        return conv.Messages;
    }
}

/// <summary>
/// Set-like facade for conversation history-loaded tracking backed by the store.
/// </summary>
public sealed class StoreHistoryLoadedSet
{
    private readonly IClientStateStore _store;
    private readonly string _agentId;

    public StoreHistoryLoadedSet(IClientStateStore store, string agentId)
    {
        _store = store;
        _agentId = agentId;
    }

    private AgentState? Agent => _store.GetAgent(_agentId);

    public bool Contains(string convId) =>
        Agent?.Conversations.GetValueOrDefault(convId)?.HistoryLoaded == true;

    public void Add(string convId)
    {
        var conv = Agent?.Conversations.GetValueOrDefault(convId);
        if (conv is not null)
            conv.HistoryLoaded = true;
    }

    public bool Remove(string convId)
    {
        var conv = Agent?.Conversations.GetValueOrDefault(convId);
        if (conv is null)
            return false;

        conv.HistoryLoaded = false;
        return true;
    }
}

/// <summary>
/// Client-side view-model for one conversation in the sidebar list.
/// In Wave 2 this is a thin facade over <see cref="ConversationState"/>.
/// </summary>
public sealed class ConversationListItemState
{
    private readonly IClientStateStore? _store;
    private readonly string? _agentId;

    public ConversationListItemState()
    {
    }

    public ConversationListItemState(IClientStateStore store, string agentId, string conversationId)
    {
        _store = store;
        _agentId = agentId;
        ConversationId = conversationId;
    }

    private ConversationState? Data =>
        _store is not null && _agentId is not null
            ? _store.GetAgent(_agentId)?.Conversations.GetValueOrDefault(ConversationId)
            : null;

    public string ConversationId { get; init; } = "";

    public string Title
    {
        get => Data?.Title ?? _title;
        set { if (Data is not null) Data.Title = value; else _title = value; }
    }
    private string _title = "New conversation";

    public bool IsDefault
    {
        get => Data?.IsDefault ?? _isDefault;
        set { if (Data is not null) Data.IsDefault = value; else _isDefault = value; }
    }
    private bool _isDefault;

    public string Status
    {
        get => Data?.Status ?? _status;
        set { if (Data is not null) Data.Status = value; else _status = value; }
    }
    private string _status = "Active";

    public string? ActiveSessionId
    {
        get => Data?.ActiveSessionId ?? _activeSessionId;
        set { if (Data is not null) Data.ActiveSessionId = value; else _activeSessionId = value; }
    }
    private string? _activeSessionId;

    public int UnreadCount
    {
        get => Data?.UnreadCount ?? _unreadCount;
        set { if (Data is not null) Data.UnreadCount = value; else _unreadCount = value; }
    }
    private int _unreadCount;

    public DateTimeOffset CreatedAt
    {
        get => Data?.CreatedAt ?? _createdAt;
        set { if (Data is not null) Data.CreatedAt = value; else _createdAt = value; }
    }
    private DateTimeOffset _createdAt;

    public DateTimeOffset UpdatedAt
    {
        get => Data?.UpdatedAt ?? _updatedAt;
        set { if (Data is not null) Data.UpdatedAt = value; else _updatedAt = value; }
    }
    private DateTimeOffset _updatedAt;

    public bool HistoryLoaded
    {
        get => Data?.HistoryLoaded ?? _historyLoaded;
        set { if (Data is not null) Data.HistoryLoaded = value; else _historyLoaded = value; }
    }
    private bool _historyLoaded;

    public bool IsLoadingHistory
    {
        get => Data?.IsLoadingHistory ?? _isLoadingHistory;
        set { if (Data is not null) Data.IsLoadingHistory = value; else _isLoadingHistory = value; }
    }
    private bool _isLoadingHistory;
}

/// <summary>
/// A conversation history entry, used when mapping REST responses into the message timeline.
/// </summary>
public sealed record ConversationHistoryItem(
    string Kind,
    string SessionId,
    DateTimeOffset Timestamp)
{
    public string? Role { get; init; }
    public string? Content { get; init; }
    public string? ToolName { get; init; }
    public string? ToolCallId { get; init; }
    public string? Reason { get; init; }

    public bool IsBoundary => Kind == "boundary";
}

/// <summary>
/// Tracks an in-progress tool invocation so we can compute duration on completion.
/// </summary>
public sealed class ActiveToolCall
{
    /// <summary>The tool-call ID from the server event.</summary>
    public required string ToolCallId { get; init; }

    /// <summary>Human-readable tool name.</summary>
    public required string ToolName { get; init; }

    /// <summary>When the tool invocation started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>The <see cref="ChatMessage.Id"/> of the ToolStart message so we can update it on ToolEnd.</summary>
    public required string MessageId { get; init; }
}

/// <summary>
/// Tracks a sub-agent spawned by a parent agent.
/// </summary>
public sealed class SubAgentInfo
{
    /// <summary>Unique sub-agent identifier.</summary>
    public required string SubAgentId { get; init; }

    /// <summary>Human-readable name of the sub-agent.</summary>
    public string? Name { get; set; }

    /// <summary>The task assigned to this sub-agent.</summary>
    public string Task { get; set; } = "";

    /// <summary>Current status: Running, Completed, Failed, Killed.</summary>
    public string Status { get; set; } = "Running";

    /// <summary>When the sub-agent was spawned.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When the sub-agent finished (if completed/failed/killed).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Summary of the sub-agent's result.</summary>
    public string? ResultSummary { get; set; }

    /// <summary>Model used by the sub-agent.</summary>
    public string? Model { get; set; }

    /// <summary>Archetype of the sub-agent.</summary>
    public string? Archetype { get; set; }
}

/// <summary>
/// A single chat message in an agent session.
/// </summary>
public sealed record ChatMessage(string Role, string Content, DateTimeOffset Timestamp)
{
    /// <summary>Stable identity for markdown caching and tool-call linking.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Tool name if this is a tool-related message.</summary>
    public string? ToolName { get; init; }

    /// <summary>Server-assigned tool-call identifier for linking start/end events.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Serialized tool arguments (JSON).</summary>
    public string? ToolArgs { get; init; }

    /// <summary>Tool execution result.</summary>
    public string? ToolResult { get; init; }

    /// <summary>Whether this message represents a tool call.</summary>
    public bool IsToolCall { get; init; }

    /// <summary>Whether the tool call ended in error.</summary>
    public bool? ToolIsError { get; init; }

    /// <summary>Elapsed wall-clock time for the tool invocation.</summary>
    public TimeSpan? ToolDuration { get; init; }

    /// <summary>Thinking content attached to this assistant message (from ThinkingDelta events).</summary>
    public string? ThinkingContent { get; init; }

    /// <summary>Message kind: "message" (default) or "boundary" (session divider).</summary>
    public string Kind { get; init; } = "message";

    /// <summary>Human-readable label for session boundary entries.</summary>
    public string? BoundaryLabel { get; init; }

    /// <summary>Session ID encoded in the boundary entry.</summary>
    public string? BoundarySessionId { get; init; }

    /// <summary>Whether this entry is a session boundary divider.</summary>
    public bool IsBoundary => Kind == "boundary";

    /// <summary>CSS class derived from the message role.</summary>
    public string CssClass => Role.ToLowerInvariant();
}
