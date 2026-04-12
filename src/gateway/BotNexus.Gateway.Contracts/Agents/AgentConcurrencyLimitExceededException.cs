using BotNexus.Domain.Primitives;

namespace BotNexus.Gateway.Abstractions.Agents;

/// <summary>
/// Thrown when an agent cannot start another session because it reached its concurrency limit.
/// </summary>
public sealed class AgentConcurrencyLimitExceededException : Exception
{
    public AgentConcurrencyLimitExceededException(AgentId agentId, int maxConcurrentSessions)
        : base($"Agent '{agentId}' has reached MaxConcurrentSessions ({maxConcurrentSessions}).")
    {
        AgentId = agentId;
        MaxConcurrentSessions = maxConcurrentSessions;
    }

    /// <summary>
    /// Gets the agent id.
    /// </summary>
    public AgentId AgentId { get; }

    /// <summary>
    /// Gets the max concurrent sessions.
    /// </summary>
    public int MaxConcurrentSessions { get; }
}
