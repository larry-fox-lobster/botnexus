namespace BotNexus.Gateway.Abstractions.Security;

/// <summary>
/// Classifies the risk level of a tool invocation.
/// </summary>
public enum ToolRiskLevel
{
    /// <summary>Tool is safe and requires no special handling.</summary>
    Safe,

    /// <summary>Tool has moderate risk — logged but not blocked.</summary>
    Moderate,

    /// <summary>Tool is dangerous and requires explicit approval.</summary>
    Dangerous
}

/// <summary>
/// Describes a tool's risk classification and approval requirements.
/// </summary>
public sealed record ToolPolicyEntry(string ToolName, ToolRiskLevel RiskLevel, bool RequiresApproval);

/// <summary>
/// Provides risk classification and approval requirements for tools.
/// Used by hook handlers to enforce tool-level security policies.
/// </summary>
public interface IToolPolicyProvider
{
    /// <summary>Returns the risk level for a given tool.</summary>
    ToolRiskLevel GetRiskLevel(string toolName);

    /// <summary>
    /// Returns whether the given tool requires explicit approval before execution.
    /// Per-agent overrides may relax or tighten the default policy.
    /// </summary>
    bool RequiresApproval(string toolName, string? agentId = null);

    /// <summary>Returns tool names that are blocked from the HTTP API surface.</summary>
    IReadOnlyList<string> GetDeniedForHttp();
}
