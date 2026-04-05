using System.Text.Json.Serialization;

namespace BotNexus.Providers.Core.Models;

/// <summary>
/// Base message type using "role" discriminator for polymorphic serialization.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "role")]
[JsonDerivedType(typeof(UserMessage), "user")]
[JsonDerivedType(typeof(AssistantMessage), "assistant")]
[JsonDerivedType(typeof(ToolResultMessage), "toolResult")]
public abstract record Message(long Timestamp);

public sealed record UserMessage(
    UserMessageContent Content,
    long Timestamp
) : Message(Timestamp);

public sealed record AssistantMessage(
    IReadOnlyList<ContentBlock> Content,
    string Api,
    string Provider,
    string ModelId,
    Usage Usage,
    StopReason StopReason,
    string? ErrorMessage,
    string? ResponseId,
    long Timestamp
) : Message(Timestamp);

public sealed record ToolResultMessage(
    string ToolCallId,
    string ToolName,
    IReadOnlyList<ContentBlock> Content,
    bool IsError,
    long Timestamp,
    object? Details = null
) : Message(Timestamp);
