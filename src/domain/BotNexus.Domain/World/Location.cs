namespace BotNexus.Domain.World;

using BotNexus.Domain.Primitives;

/// <summary>
/// Represents location.
/// </summary>
public sealed record Location
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public required LocationType Type { get; init; }
    /// <summary>
    /// Gets or sets the path.
    /// </summary>
    public string? Path { get; init; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}
