using BotNexus.Domain.Primitives;
using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Abstractions.Conversations;

/// <summary>
/// Resolves the active conversation and session for an inbound message,
/// and determines which channel bindings should receive outbound replies.
/// </summary>
public interface IConversationRouter
{
    /// <summary>
    /// Resolves or creates the conversation for an inbound message.
    /// Uses (AgentId, ChannelType, ChannelAddress, ThreadId?) as the lookup key.
    /// Falls back to the agent's default conversation if no binding matches.
    /// Stamps Session.ConversationId when creating/resolving sessions.
    /// </summary>
    Task<ConversationRoutingResult> ResolveInboundAsync(
        AgentId agentId,
        ChannelKey channelType,
        string channelAddress,
        string? threadId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns channel bindings that should receive outbound fan-out for a session.
    /// Excludes the originating binding from fan-out to prevent echo.
    /// Only returns bindings with BindingMode.Interactive or BindingMode.NotifyOnly.
    /// </summary>
    Task<IReadOnlyList<ChannelBinding>> GetOutboundBindingsAsync(
        SessionId sessionId,
        string originatingChannelAddress,
        CancellationToken ct = default);
}

/// <summary>
/// Result of resolving an inbound message to a conversation and session.
/// </summary>
/// <param name="Conversation">The resolved or created conversation.</param>
/// <param name="SessionId">The session to dispatch the message into.</param>
/// <param name="IsNewSession">True if a new session was created for this message.</param>
public sealed record ConversationRoutingResult(
    Conversation Conversation,
    SessionId SessionId,
    bool IsNewSession);
