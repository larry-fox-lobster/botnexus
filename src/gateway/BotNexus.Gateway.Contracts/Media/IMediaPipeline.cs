using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Abstractions.Media;

/// <summary>
/// Orchestrates media processing by routing content parts through registered handlers.
/// </summary>
public interface IMediaPipeline
{
    /// <summary>
    /// Processes all content parts in a message through registered media handlers.
    /// Returns the processed parts (unchanged parts pass through as-is).
    /// </summary>
    Task<IReadOnlyList<MessageContentPart>> ProcessAsync(
        IReadOnlyList<MessageContentPart> contentParts,
        MediaProcessingContext context);
}
