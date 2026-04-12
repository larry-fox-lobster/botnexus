namespace BotNexus.Domain.Primitives;

/// <summary>
/// Represents session participant.
/// </summary>
public sealed record SessionParticipant
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public required ParticipantType Type { get; init; }
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Gets or sets the world id.
    /// </summary>
    public string? WorldId { get; init; }
    /// <summary>
    /// Gets or sets the role.
    /// </summary>
    public string? Role { get; init; }
}

/// <summary>
/// Specifies supported values for participant type.
/// </summary>
public enum ParticipantType
{
    User,
    Agent
}
