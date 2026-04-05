namespace BotNexus.Providers.Core.Models;

public record UsageCost(
    decimal Input,
    decimal Output,
    decimal CacheRead,
    decimal CacheWrite,
    decimal Total
);

public sealed record Usage
{
    public int Input { get; init; }
    public int Output { get; init; }
    public int CacheRead { get; init; }
    public int CacheWrite { get; init; }
    public int TotalTokens { get; init; }
    public UsageCost Cost { get; init; } = new(0, 0, 0, 0, 0);

    public static Usage Empty() => new();
}
