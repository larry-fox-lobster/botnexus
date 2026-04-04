using BotNexus.Providers.Core.Models;

namespace BotNexus.Providers.Core;

/// <summary>
/// Base options shared by all providers. Maps to pi-mono's StreamOptions.
/// CancellationToken replaces AbortSignal from the TypeScript version.
/// </summary>
public class StreamOptions
{
    public float? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public CancellationToken CancellationToken { get; set; }
    public string? ApiKey { get; set; }
    public Transport Transport { get; set; } = Transport.Sse;
    public CacheRetention CacheRetention { get; set; } = CacheRetention.Short;
    public string? SessionId { get; set; }
    public Func<object, LlmModel, Task<object?>>? OnPayload { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public int MaxRetryDelayMs { get; set; } = 60000;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Extended options with reasoning/thinking support. Maps to pi-mono's SimpleStreamOptions.
/// </summary>
public class SimpleStreamOptions : StreamOptions
{
    public ThinkingLevel? Reasoning { get; set; }
    public ThinkingBudgets? ThinkingBudgets { get; set; }
}
