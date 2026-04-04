namespace BotNexus.Providers.Base;

/// <summary>
/// Registry of GitHub Copilot models.
/// Ported from Pi's models.generated.ts.
/// </summary>
public static class CopilotModels
{
    private const string CopilotBaseUrl = "https://api.individual.githubcopilot.com";
    
    private static readonly Dictionary<string, string> CopilotHeaders = new()
    {
        ["User-Agent"] = "GitHubCopilotChat/0.35.0",
        ["Editor-Version"] = "vscode/1.107.0",
        ["Editor-Plugin-Version"] = "copilot-chat/0.35.0",
        ["Copilot-Integration-Id"] = "vscode-chat"
    };
    
    private static readonly string[] TextInput = ["text"];
    private static readonly string[] TextAndImageInput = ["text", "image"];
    
    /// <summary>All registered Copilot models.</summary>
    public static IReadOnlyList<ModelDefinition> All { get; } = new[]
    {
        // Claude models - use Anthropic Messages API
        new ModelDefinition(
            Id: "claude-haiku-4.5",
            Name: "Claude Haiku 4.5",
            Api: "anthropic-messages",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextAndImageInput,
            ContextWindow: 200000,
            MaxTokens: 8192
        ),
        new ModelDefinition(
            Id: "claude-opus-4.5",
            Name: "Claude Opus 4.5",
            Api: "anthropic-messages",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextAndImageInput,
            ContextWindow: 200000,
            MaxTokens: 16384
        ),
        new ModelDefinition(
            Id: "claude-opus-4.6",
            Name: "Claude Opus 4.6",
            Api: "anthropic-messages",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: true,
            Input: TextAndImageInput,
            ContextWindow: 1000000,
            MaxTokens: 64000
        ),
        new ModelDefinition(
            Id: "claude-sonnet-4",
            Name: "Claude Sonnet 4",
            Api: "anthropic-messages",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextAndImageInput,
            ContextWindow: 200000,
            MaxTokens: 8192
        ),
        new ModelDefinition(
            Id: "claude-sonnet-4.5",
            Name: "Claude Sonnet 4.5",
            Api: "anthropic-messages",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextAndImageInput,
            ContextWindow: 200000,
            MaxTokens: 8192
        ),
        new ModelDefinition(
            Id: "claude-sonnet-4.6",
            Name: "Claude Sonnet 4.6",
            Api: "anthropic-messages",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: true,
            Input: TextAndImageInput,
            ContextWindow: 200000,
            MaxTokens: 8192
        ),
        
        // GPT models - use OpenAI Completions API
        new ModelDefinition(
            Id: "gpt-4o",
            Name: "GPT-4o",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextAndImageInput,
            ContextWindow: 128000,
            MaxTokens: 16384
        ),
        new ModelDefinition(
            Id: "gpt-4o-mini",
            Name: "GPT-4o mini",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextAndImageInput,
            ContextWindow: 128000,
            MaxTokens: 16384
        ),
        new ModelDefinition(
            Id: "gpt-4.1",
            Name: "GPT-4.1",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextInput,
            ContextWindow: 128000,
            MaxTokens: 16384
        ),
        new ModelDefinition(
            Id: "o1",
            Name: "o1",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: true,
            Input: TextAndImageInput,
            ContextWindow: 200000,
            MaxTokens: 100000
        ),
        new ModelDefinition(
            Id: "o1-mini",
            Name: "o1-mini",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: true,
            Input: TextInput,
            ContextWindow: 128000,
            MaxTokens: 65536
        ),
        new ModelDefinition(
            Id: "o3",
            Name: "o3",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: true,
            Input: TextInput,
            ContextWindow: 200000,
            MaxTokens: 100000
        ),
        new ModelDefinition(
            Id: "o3-mini",
            Name: "o3-mini",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: true,
            Input: TextInput,
            ContextWindow: 200000,
            MaxTokens: 100000
        ),
        new ModelDefinition(
            Id: "o4-mini",
            Name: "o4-mini",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: true,
            Input: TextInput,
            ContextWindow: 200000,
            MaxTokens: 100000
        ),
        
        // GPT-5 models - use OpenAI Responses API
        new ModelDefinition(
            Id: "gpt-5",
            Name: "GPT-5",
            Api: "openai-responses",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextInput,
            ContextWindow: 200000,
            MaxTokens: 100000
        ),
        new ModelDefinition(
            Id: "gpt-5-mini",
            Name: "GPT-5 mini",
            Api: "openai-responses",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextInput,
            ContextWindow: 200000,
            MaxTokens: 100000
        ),
        new ModelDefinition(
            Id: "gpt-5.1",
            Name: "GPT-5.1",
            Api: "openai-responses",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextInput,
            ContextWindow: 200000,
            MaxTokens: 100000
        ),
        new ModelDefinition(
            Id: "gpt-5.2",
            Name: "GPT-5.2",
            Api: "openai-responses",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextInput,
            ContextWindow: 200000,
            MaxTokens: 100000
        ),
        new ModelDefinition(
            Id: "gpt-5.2-codex",
            Name: "GPT-5.2-Codex",
            Api: "openai-responses",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextInput,
            ContextWindow: 200000,
            MaxTokens: 100000
        ),
        new ModelDefinition(
            Id: "gpt-5.4",
            Name: "GPT-5.4",
            Api: "openai-responses",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextInput,
            ContextWindow: 200000,
            MaxTokens: 100000
        ),
        new ModelDefinition(
            Id: "gpt-5.4-mini",
            Name: "GPT-5.4 mini",
            Api: "openai-responses",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextInput,
            ContextWindow: 200000,
            MaxTokens: 100000
        ),
        
        // Gemini models - use OpenAI Completions API
        new ModelDefinition(
            Id: "gemini-2.5-pro",
            Name: "Gemini 2.5 Pro",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextAndImageInput,
            ContextWindow: 1000000,
            MaxTokens: 8192
        ),
        new ModelDefinition(
            Id: "gemini-3-flash-preview",
            Name: "Gemini 3 Flash Preview",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextAndImageInput,
            ContextWindow: 1000000,
            MaxTokens: 8192
        ),
        new ModelDefinition(
            Id: "gemini-3-pro-preview",
            Name: "Gemini 3 Pro Preview",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextAndImageInput,
            ContextWindow: 2000000,
            MaxTokens: 8192
        ),
        new ModelDefinition(
            Id: "gemini-3.1-pro-preview",
            Name: "Gemini 3.1 Pro Preview",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextAndImageInput,
            ContextWindow: 2000000,
            MaxTokens: 8192
        ),
        
        // Grok - use OpenAI Completions API
        new ModelDefinition(
            Id: "grok-code-fast-1",
            Name: "Grok Code Fast 1",
            Api: "openai-completions",
            Provider: "github-copilot",
            BaseUrl: CopilotBaseUrl,
            Headers: CopilotHeaders,
            Reasoning: false,
            Input: TextInput,
            ContextWindow: 131072,
            MaxTokens: 32768
        )
    };
    
    private static readonly Dictionary<string, ModelDefinition> _modelsById = 
        All.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
    
    /// <summary>Resolves a model by ID. Throws if not found.</summary>
    public static ModelDefinition Resolve(string modelId)
    {
        if (_modelsById.TryGetValue(modelId, out var model))
            return model;
        
        throw new ArgumentException($"Unknown model ID: {modelId}. Available models: {string.Join(", ", _modelsById.Keys)}", nameof(modelId));
    }
    
    /// <summary>Tries to resolve a model by ID.</summary>
    public static bool TryResolve(string modelId, out ModelDefinition? model)
    {
        return _modelsById.TryGetValue(modelId, out model);
    }
}
