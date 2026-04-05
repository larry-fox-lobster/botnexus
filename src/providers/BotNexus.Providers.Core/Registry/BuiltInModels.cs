using BotNexus.Providers.Core.Compatibility;
using BotNexus.Providers.Core.Models;

namespace BotNexus.Providers.Core.Registry;

/// <summary>
/// Built-in model definitions ported from pi-mono's models.generated.ts.
/// Registered at startup by calling RegisterAll().
/// </summary>
public static class BuiltInModels
{
    private static readonly IReadOnlyDictionary<string, string> CopilotHeaders = new Dictionary<string, string>
    {
        ["User-Agent"] = "GitHubCopilotChat/0.35.0",
        ["Editor-Version"] = "vscode/1.107.0",
        ["Editor-Plugin-Version"] = "copilot-chat/0.35.0",
        ["Copilot-Integration-Id"] = "vscode-chat"
    };

    private static readonly OpenAICompletionsCompat CopilotCompletionsCompat = new()
    {
        SupportsStore = false,
        SupportsDeveloperRole = false,
        SupportsReasoningEffort = false
    };

    private const string CopilotBaseUrl = "https://api.individual.githubcopilot.com";
    private static readonly ModelCost FreeCost = new(0, 0, 0, 0);

    /// <summary>Register all built-in models with the global ModelRegistry.</summary>
    public static void RegisterAll()
    {
        RegisterCopilotModels();
    }

    private static void RegisterCopilotModels()
    {
        Register("github-copilot", "claude-haiku-4.5", "Claude Haiku 4.5", "anthropic-messages", true, ["text", "image"], 144000, 32000);
        Register("github-copilot", "claude-opus-4.5", "Claude Opus 4.5", "anthropic-messages", true, ["text", "image"], 160000, 32000);
        Register("github-copilot", "claude-opus-4.6", "Claude Opus 4.6", "anthropic-messages", true, ["text", "image"], 1000000, 64000);
        Register("github-copilot", "claude-sonnet-4", "Claude Sonnet 4", "anthropic-messages", true, ["text", "image"], 216000, 16000);
        Register("github-copilot", "claude-sonnet-4.5", "Claude Sonnet 4.5", "anthropic-messages", true, ["text", "image"], 144000, 32000);
        Register("github-copilot", "claude-sonnet-4.6", "Claude Sonnet 4.6", "anthropic-messages", true, ["text", "image"], 1000000, 32000);

        Register("github-copilot", "gemini-2.5-pro", "Gemini 2.5 Pro", "openai-completions", false, ["text", "image"], 128000, 64000, CopilotCompletionsCompat);
        Register("github-copilot", "gemini-3-flash-preview", "Gemini 3 Flash", "openai-completions", true, ["text", "image"], 128000, 64000, CopilotCompletionsCompat);
        Register("github-copilot", "gemini-3-pro-preview", "Gemini 3 Pro Preview", "openai-completions", true, ["text", "image"], 128000, 64000, CopilotCompletionsCompat);
        Register("github-copilot", "gemini-3.1-pro-preview", "Gemini 3.1 Pro Preview", "openai-completions", true, ["text", "image"], 128000, 64000, CopilotCompletionsCompat);

        Register("github-copilot", "gpt-4.1", "GPT-4.1", "openai-completions", false, ["text", "image"], 128000, 16384, CopilotCompletionsCompat);
        Register("github-copilot", "gpt-4o", "GPT-4o", "openai-completions", false, ["text", "image"], 128000, 4096, CopilotCompletionsCompat);

        Register("github-copilot", "gpt-5", "GPT-5", "openai-responses", true, ["text", "image"], 128000, 128000);
        Register("github-copilot", "gpt-5-mini", "GPT-5-mini", "openai-responses", true, ["text", "image"], 264000, 64000);
        Register("github-copilot", "gpt-5.1", "GPT-5.1", "openai-responses", true, ["text", "image"], 264000, 64000);
        Register("github-copilot", "gpt-5.1-codex", "GPT-5.1-Codex", "openai-responses", true, ["text", "image"], 400000, 128000);
        Register("github-copilot", "gpt-5.1-codex-max", "GPT-5.1-Codex-max", "openai-responses", true, ["text", "image"], 400000, 128000);
        Register("github-copilot", "gpt-5.1-codex-mini", "GPT-5.1-Codex-mini", "openai-responses", true, ["text", "image"], 400000, 128000);
        Register("github-copilot", "gpt-5.2", "GPT-5.2", "openai-responses", true, ["text", "image"], 264000, 64000);
        Register("github-copilot", "gpt-5.2-codex", "GPT-5.2-Codex", "openai-responses", true, ["text", "image"], 400000, 128000);
        Register("github-copilot", "gpt-5.3-codex", "GPT-5.3-Codex", "openai-responses", true, ["text", "image"], 400000, 128000);
        Register("github-copilot", "gpt-5.4", "GPT-5.4", "openai-responses", true, ["text", "image"], 400000, 128000);
        Register("github-copilot", "gpt-5.4-mini", "GPT-5.4 mini", "openai-responses", true, ["text", "image"], 400000, 128000);

        Register("github-copilot", "grok-code-fast-1", "Grok Code Fast 1", "openai-completions", true, ["text"], 128000, 64000, CopilotCompletionsCompat);
    }

    private static void Register(
        string provider,
        string id,
        string name,
        string api,
        bool reasoning,
        IReadOnlyList<string> input,
        int contextWindow,
        int maxTokens,
        OpenAICompletionsCompat? compat = null)
    {
        ModelRegistry.Register(provider, new LlmModel(
            Id: id,
            Name: name,
            Api: api,
            Provider: provider,
            BaseUrl: CopilotBaseUrl,
            Reasoning: reasoning,
            Input: input,
            Cost: FreeCost,
            ContextWindow: contextWindow,
            MaxTokens: maxTokens,
            Headers: CopilotHeaders,
            Compat: compat));
    }
}
