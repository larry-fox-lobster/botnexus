namespace BotNexus.Core.Abstractions;

/// <summary>Execution context provided to each cron job at runtime.</summary>
public sealed class CronJobContext
{
    public required string JobName { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTimeOffset ScheduledTime { get; init; }
    public required DateTimeOffset ActualTime { get; init; }
    public required IServiceProvider Services { get; init; }
}
