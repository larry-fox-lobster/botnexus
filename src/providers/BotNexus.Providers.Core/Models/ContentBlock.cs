using System.Text.Json.Serialization;

namespace BotNexus.Providers.Core.Models;

/// <summary>
/// Base content block for message content arrays.
/// Uses "type" discriminator for polymorphic JSON serialization.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContent), "text")]
[JsonDerivedType(typeof(ThinkingContent), "thinking")]
[JsonDerivedType(typeof(ImageContent), "image")]
[JsonDerivedType(typeof(ToolCallContent), "toolCall")]
public abstract record ContentBlock;

public sealed record TextContent(
    string Text,
    string? TextSignature = null
) : ContentBlock;

public sealed record ThinkingContent(
    string Thinking,
    string? ThinkingSignature = null,
    bool? Redacted = null
) : ContentBlock;

public sealed record ImageContent(
    string Data,
    string MimeType
) : ContentBlock;

public sealed record ToolCallContent(
    string Id,
    string Name,
    Dictionary<string, object?> Arguments,
    string? ThoughtSignature = null
) : ContentBlock;
