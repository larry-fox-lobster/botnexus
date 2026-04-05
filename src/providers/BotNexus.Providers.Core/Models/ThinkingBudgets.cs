namespace BotNexus.Providers.Core.Models;

/// <summary>
/// Custom token budgets for each thinking level (token-based providers only).
/// </summary>
public record ThinkingBudgets
{
    public int? Minimal { get; init; }
    public int? Low { get; init; }
    public int? Medium { get; init; }
    public int? High { get; init; }
    public int? ExtraHigh { get; init; }
}
