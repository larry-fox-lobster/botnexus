namespace BotNexus.Core.Abstractions;

/// <summary>Result of a cron job execution.</summary>
public sealed record CronJobResult(
    bool Success,
    string? Output = null,
    string? Error = null,
    TimeSpan Duration = default,
    IReadOnlyDictionary<string, object>? Metadata = null);
