using BotNexus.Gateway.Abstractions.Security;
using BotNexus.Gateway.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway.Security;

/// <summary>
/// Default tool policy provider with built-in dangerous tool classifications
/// derived from the OpenClaw security baseline.
/// </summary>
public sealed class DefaultToolPolicyProvider : IToolPolicyProvider
{
    private static readonly HashSet<string> DangerousTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "exec", "write", "edit", "process", "bash"
    };

    private static readonly List<string> HttpDeniedTools =
    [
        "sessions_spawn",
        "sessions_send",
        "cron",
        "gateway",
        "whatsapp_login"
    ];

    private static readonly HashSet<string> HttpDeniedLookup = new(HttpDeniedTools, StringComparer.OrdinalIgnoreCase);

    private readonly PlatformConfig _config;
    private readonly ILogger<DefaultToolPolicyProvider> _logger;

    public DefaultToolPolicyProvider(
        IOptions<PlatformConfig> config,
        ILogger<DefaultToolPolicyProvider> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public ToolRiskLevel GetRiskLevel(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (HttpDeniedLookup.Contains(toolName))
            return ToolRiskLevel.Dangerous;

        if (DangerousTools.Contains(toolName))
            return ToolRiskLevel.Dangerous;

        return ToolRiskLevel.Safe;
    }

    /// <inheritdoc />
    public bool RequiresApproval(string toolName, string? agentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        // Check per-agent overrides from config
        if (agentId is not null)
        {
            var agentPolicy = GetAgentToolPolicy(agentId);
            if (agentPolicy is not null)
            {
                // Explicitly trusted tools skip approval
                if (agentPolicy.NeverApprove?.Contains(toolName, StringComparer.OrdinalIgnoreCase) == true)
                {
                    _logger.LogDebug(
                        "Tool {ToolName} skips approval for agent {AgentId} (per-agent NeverApprove override)",
                        toolName, agentId);
                    return false;
                }

                // Explicitly requiring approval overrides default
                if (agentPolicy.AlwaysApprove?.Contains(toolName, StringComparer.OrdinalIgnoreCase) == true)
                    return true;
            }
        }

        return DangerousTools.Contains(toolName);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetDeniedForHttp() => HttpDeniedTools;

    /// <summary>
    /// Checks whether a tool is completely blocked for a specific agent.
    /// </summary>
    internal bool IsDenied(string toolName, string? agentId)
    {
        if (agentId is null)
            return false;

        var agentPolicy = GetAgentToolPolicy(agentId);
        return agentPolicy?.Denied?.Contains(toolName, StringComparer.OrdinalIgnoreCase) == true;
    }

    private ToolPolicyConfig? GetAgentToolPolicy(string agentId)
    {
        if (_config.Agents is null || !_config.Agents.TryGetValue(agentId, out var agentConfig))
            return null;

        return agentConfig.ToolPolicy;
    }
}
