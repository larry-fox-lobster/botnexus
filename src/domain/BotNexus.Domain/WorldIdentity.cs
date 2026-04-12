namespace BotNexus.Domain;

/// <summary>
/// Represents world identity.
/// </summary>
public sealed record WorldIdentity
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// Gets or sets the emoji.
    /// </summary>
    public string? Emoji { get; init; }
}
