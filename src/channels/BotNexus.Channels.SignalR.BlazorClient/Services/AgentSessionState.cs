namespace BotNexus.Channels.SignalR.BlazorClient.Services;

/// <summary>
/// Independent state for one agent's session. No global shared state —
/// each chat panel is scoped to exactly one <see cref="AgentSessionState"/>.
/// </summary>
public sealed class AgentSessionState
{
    /// <summary>The agent's unique identifier.</summary>
    public required string AgentId { get; init; }

    /// <summary>Human-friendly display name for the agent.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Active session ID (set after first message exchange).</summary>
    public string? SessionId { get; set; }

    /// <summary>Channel type for this session.</summary>
    public string? ChannelType { get; set; }

    /// <summary>All messages in this session, in chronological order.</summary>
    public List<ChatMessage> Messages { get; } = [];

    /// <summary>Whether the agent is currently streaming a response.</summary>
    public bool IsStreaming { get; set; }

    /// <summary>Buffer for the in-progress streaming response.</summary>
    public string CurrentStreamBuffer { get; set; } = "";

    /// <summary>Whether the hub connection is active.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Count of unread messages while this agent's tab is not active.</summary>
    public int UnreadCount { get; set; }
}

/// <summary>
/// A single chat message in an agent session.
/// </summary>
public sealed record ChatMessage(string Role, string Content, DateTimeOffset Timestamp)
{
    /// <summary>Tool name if this is a tool-related message.</summary>
    public string? ToolName { get; init; }

    /// <summary>Serialized tool arguments.</summary>
    public string? ToolArgs { get; init; }

    /// <summary>Tool execution result.</summary>
    public string? ToolResult { get; init; }

    /// <summary>Whether this message represents a tool call.</summary>
    public bool IsToolCall { get; init; }

    /// <summary>CSS class derived from the message role.</summary>
    public string CssClass => Role.ToLowerInvariant();
}
