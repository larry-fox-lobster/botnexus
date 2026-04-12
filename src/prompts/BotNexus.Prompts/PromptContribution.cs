namespace BotNexus.Prompts;

public sealed record PromptContribution
{
    public int? Order { get; init; }

    public IReadOnlyList<string> Lines { get; init; } = [];

    public string? SectionHeading { get; init; }
}
