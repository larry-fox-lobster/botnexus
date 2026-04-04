using BotNexus.Core.Models;

namespace BotNexus.Core.Abstractions;

/// <summary>
/// Builds the full agent context (system prompt + message array) for LLM calls.
/// Assembles identity, workspace files, memory, tools, and runtime metadata.
/// </summary>
public interface IContextBuilder
{
    /// <summary>
    /// Builds the system prompt from workspace files, memory, and runtime context.
    /// </summary>
    Task<string> BuildSystemPromptAsync(string agentName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the complete message array for an LLM call, including system prompt,
    /// session history, and the current user message with runtime metadata.
    /// </summary>
    Task<List<ChatMessage>> BuildMessagesAsync(
        string agentName,
        IReadOnlyList<ChatMessage> history,
        string currentMessage,
        string? channel = null,
        string? chatId = null,
        CancellationToken cancellationToken = default);
}
