namespace BotNexus.Providers.Core.Models;

public record UsageCost(
    decimal Input,
    decimal Output,
    decimal CacheRead,
    decimal CacheWrite,
    decimal Total
);

public sealed class Usage
{
    public int Input { get; set; }
    public int Output { get; set; }
    public int CacheRead { get; set; }
    public int CacheWrite { get; set; }
    public int TotalTokens { get; set; }
    public UsageCost Cost { get; set; } = new(0, 0, 0, 0, 0);

    public static Usage Empty() => new()
    {
        Cost = new UsageCost(0, 0, 0, 0, 0)
    };
}
