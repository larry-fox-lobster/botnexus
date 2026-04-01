using BotNexus.Core.Configuration;

namespace BotNexus.Providers.Copilot;

public sealed class CopilotConfig : ProviderConfig
{
    public const string DefaultApiBaseUrl = "https://api.githubcopilot.com";
    public const string DefaultModelName = "gpt-4o";
    public const string DefaultOAuthClientId = "Iv1.b507a08c87ecfe98";

    public CopilotConfig()
    {
        Auth = "oauth";
        ApiBase = DefaultApiBaseUrl;
        DefaultModel = DefaultModelName;
    }

    public string OAuthClientId { get; set; } = DefaultOAuthClientId;
}
