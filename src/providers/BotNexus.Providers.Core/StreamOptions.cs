using BotNexus.Providers.Core.Models;

namespace BotNexus.Providers.Core;

/// <summary>
/// Base options shared by all providers. Maps to pi-mono's StreamOptions.
/// CancellationToken replaces AbortSignal from the TypeScript version.
/// </summary>
public record class StreamOptions
{
    public float? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public string? ApiKey { get; init; }
    public Transport Transport { get; init; } = Transport.Sse;
    public CacheRetention CacheRetention { get; init; } = CacheRetention.Short;
    public string? SessionId { get; init; }
    public Func<object, LlmModel, Task<object?>>? OnPayload { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public int MaxRetryDelayMs { get; init; } = 60000;
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Extended options with reasoning/thinking support. Maps to pi-mono's SimpleStreamOptions.
/// </summary>
public record class SimpleStreamOptions : StreamOptions
{
    public ThinkingLevel? Reasoning { get; init; }
    public ThinkingBudgets? ThinkingBudgets { get; init; }
}
