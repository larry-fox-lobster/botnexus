namespace BotNexus.Domain.World;

using BotNexus.Domain.Primitives;

/// <summary>
/// Represents cross world permission.
/// </summary>
public sealed record CrossWorldPermission
{
    /// <summary>
    /// Gets or sets the target world id.
    /// </summary>
    public required string TargetWorldId { get; init; }
    /// <summary>
    /// Gets or sets the allowed agents.
    /// </summary>
    public IReadOnlyList<AgentId>? AllowedAgents { get; init; }
    /// <summary>
    /// Gets or sets the allow inbound.
    /// </summary>
    public bool AllowInbound { get; init; } = true;
    /// <summary>
    /// Gets or sets the allow outbound.
    /// </summary>
    public bool AllowOutbound { get; init; } = true;
}
