namespace BotNexus.Prompts;

/// <summary>
/// Represents prompt contribution.
/// </summary>
public sealed record PromptContribution
{
    /// <summary>
    /// Gets or sets the order.
    /// </summary>
    public int? Order { get; init; }

    /// <summary>
    /// Gets or sets the lines.
    /// </summary>
    public IReadOnlyList<string> Lines { get; init; } = [];

    /// <summary>
    /// Gets or sets the section heading.
    /// </summary>
    public string? SectionHeading { get; init; }
}
