namespace BotNexus.Providers.Base;

/// <summary>
/// Defines a specific model with its API requirements and capabilities.
/// This is the equivalent of Pi's Model&lt;T&gt; type.
/// </summary>
/// <param name="Id">Model identifier (e.g., "claude-opus-4.6")</param>
/// <param name="Name">Human-readable name (e.g., "Claude Opus 4.6")</param>
/// <param name="Api">API format handler type (e.g., "anthropic-messages", "openai-completions", "openai-responses")</param>
/// <param name="Provider">Provider name (e.g., "github-copilot", "openai", "anthropic")</param>
/// <param name="BaseUrl">API base URL (e.g., "https://api.individual.githubcopilot.com")</param>
/// <param name="Headers">Provider-specific HTTP headers</param>
/// <param name="Reasoning">Whether the model supports thinking/reasoning modes</param>
/// <param name="Input">Supported input types (e.g., ["text", "image"])</param>
/// <param name="ContextWindow">Maximum context window size in tokens</param>
/// <param name="MaxTokens">Maximum tokens that can be generated</param>
public record ModelDefinition(
    string Id,
    string Name,
    string Api,
    string Provider,
    string BaseUrl,
    IReadOnlyDictionary<string, string>? Headers,
    bool Reasoning,
    IReadOnlyList<string> Input,
    int ContextWindow,
    int MaxTokens);
