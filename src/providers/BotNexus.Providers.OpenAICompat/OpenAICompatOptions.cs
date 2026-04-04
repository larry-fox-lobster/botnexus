namespace BotNexus.Providers.OpenAICompat;

/// <summary>
/// Options specific to OpenAI-compatible providers.
/// </summary>
public class OpenAICompatOptions : BotNexus.Providers.Core.StreamOptions
{
    public string? ToolChoice { get; set; }
    public string? ReasoningEffort { get; set; }
}
