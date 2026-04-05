namespace BotNexus.Gateway.Configuration;

/// <summary>
/// Platform-wide BotNexus configuration stored at ~/.botnexus/config.json.
/// </summary>
public sealed class PlatformConfig
{
    /// <summary>Default Gateway listen URL.</summary>
    public string? ListenUrl { get; set; }

    /// <summary>Default agent to use when none specified.</summary>
    public string? DefaultAgentId { get; set; }

    /// <summary>Path to agents configuration directory.</summary>
    public string? AgentsDirectory { get; set; }

    /// <summary>Path to sessions storage directory.</summary>
    public string? SessionsDirectory { get; set; }

    /// <summary>API key for Gateway authentication (null = dev mode, no auth).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Logging level.</summary>
    public string? LogLevel { get; set; }

    /// <summary>Provider configurations keyed by provider name.</summary>
    public Dictionary<string, ProviderConfig>? Providers { get; set; }
}

/// <summary>Provider-specific configuration.</summary>
public sealed class ProviderConfig
{
    /// <summary>API key or reference to auth.json entry.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Base URL override.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Default model for this provider.</summary>
    public string? DefaultModel { get; set; }
}
