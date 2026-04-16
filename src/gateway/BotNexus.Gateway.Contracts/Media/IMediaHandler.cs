using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Abstractions.Media;

/// <summary>
/// Context for media processing operations.
/// </summary>
public sealed record MediaProcessingContext
{
    /// <summary>Session ID the message belongs to.</summary>
    public required string SessionId { get; init; }

    /// <summary>Channel the message arrived from.</summary>
    public required string ChannelType { get; init; }

    /// <summary>Cancellation token.</summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Result of processing a single content part.
/// </summary>
public sealed record MediaProcessingResult
{
    /// <summary>The processed content part (may be same as input if no processing needed).</summary>
    public required MessageContentPart ProcessedPart { get; init; }

    /// <summary>Whether this handler actually transformed the content.</summary>
    public bool WasTransformed { get; init; }

    /// <summary>Optional metadata about the processing (duration, model used, etc.).</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}

/// <summary>
/// Handles processing of specific media content types.
/// Implementations are discovered via the extension system.
/// </summary>
public interface IMediaHandler
{
    /// <summary>Display name for logging/diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Priority for handler ordering. Lower values execute first.
    /// Default is 100.
    /// </summary>
    int Priority => 100;

    /// <summary>
    /// Determines whether this handler can process the given content part.
    /// </summary>
    bool CanHandle(MessageContentPart contentPart);

    /// <summary>
    /// Processes a content part, potentially transforming it.
    /// </summary>
    Task<MediaProcessingResult> ProcessAsync(
        MessageContentPart contentPart,
        MediaProcessingContext context);
}
