using BotNexus.Providers.Core.Models;
using BotNexus.Providers.Core.Streaming;

namespace BotNexus.Providers.Core.Registry;

/// <summary>
/// API provider contract. Each provider handles a specific API format
/// (e.g., "anthropic-messages", "openai-completions", "openai-responses").
/// </summary>
public interface IApiProvider
{
    string Api { get; }
    LlmStream Stream(LlmModel model, Context context, StreamOptions? options = null);
    LlmStream StreamSimple(LlmModel model, Context context, SimpleStreamOptions? options = null);
}
