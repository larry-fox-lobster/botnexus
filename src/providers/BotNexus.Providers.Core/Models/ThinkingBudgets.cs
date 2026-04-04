namespace BotNexus.Providers.Core.Models;

/// <summary>
/// Token budget for a single thinking level.
/// </summary>
public record ThinkingBudgetLevel(int ThinkingBudget, int MaxTokens);

/// <summary>
/// Custom token budgets for each thinking level (token-based providers only).
/// </summary>
public record ThinkingBudgets
{
    public ThinkingBudgetLevel? Minimal { get; init; }
    public ThinkingBudgetLevel? Low { get; init; }
    public ThinkingBudgetLevel? Medium { get; init; }
    public ThinkingBudgetLevel? High { get; init; }
    public ThinkingBudgetLevel? ExtraHigh { get; init; }
}
