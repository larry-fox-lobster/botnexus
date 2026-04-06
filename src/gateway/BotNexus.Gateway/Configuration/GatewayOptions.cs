namespace BotNexus.Gateway.Configuration;

public sealed class GatewayOptions
{
    /// <summary>
    /// Optional default agent used when no explicit target or session-bound agent is available.
    /// </summary>
    public string? DefaultAgentId { get; set; }

    /// <summary>
    /// Maximum allowed depth for cross-agent/sub-agent call chains.
    /// </summary>
    public int MaxCallChainDepth { get; set; } = 10;
}
