namespace BotNexus.Core.Abstractions;

/// <summary>Historical record of a single cron job execution.</summary>
public sealed record CronJobExecution(
    string JobName,
    string CorrelationId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool Success,
    string? Output,
    string? Error);
