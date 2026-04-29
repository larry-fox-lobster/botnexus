using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

/// <summary>
/// Extension methods for building <see cref="AgentSessionState"/> instances
/// with tool-call messages for testing expand/collapse behaviour.
/// </summary>
internal static class ToolCallTestExtensions
{
    /// <summary>
    /// Adds a completed tool-call message to the session state.
    /// </summary>
    public static AgentSessionState WithToolCall(
        this AgentSessionState state,
        string toolName = "search_code",
        string? toolArgs = """{"query": "hello"}""",
        string? toolResult = "Found 3 results.",
        bool isError = false,
        TimeSpan? duration = null)
    {
        EnsureConversationStore(state).Add(new ChatMessage("Tool", $"✅ {toolName} completed", DateTimeOffset.UtcNow)
        {
            ToolName = toolName,
            ToolCallId = Guid.NewGuid().ToString("N"),
            ToolArgs = toolArgs,
            ToolResult = toolResult,
            IsToolCall = true,
            ToolIsError = isError,
            ToolDuration = duration ?? TimeSpan.FromMilliseconds(450)
        });

        return state;
    }

    /// <summary>
    /// Adds an in-progress (pending) tool-call message to the session state.
    /// </summary>
    public static AgentSessionState WithPendingToolCall(
        this AgentSessionState state,
        string toolName = "run_query")
    {
        EnsureConversationStore(state).Add(new ChatMessage("Tool", $"⏳ Calling {toolName}…", DateTimeOffset.UtcNow)
        {
            ToolName = toolName,
            ToolCallId = Guid.NewGuid().ToString("N"),
            IsToolCall = true
        });

        return state;
    }

    private static List<ChatMessage> EnsureConversationStore(AgentSessionState state)
    {
        if (state.ActiveConversationId is null)
        {
            state.ActiveConversationId = "test-conv-1";
        }
        if (!state.ConversationMessageStores.TryGetValue(state.ActiveConversationId, out var msgs))
        {
            msgs = new List<ChatMessage>();
            state.ConversationMessageStores[state.ActiveConversationId] = msgs;
        }
        return msgs;
    }
}
