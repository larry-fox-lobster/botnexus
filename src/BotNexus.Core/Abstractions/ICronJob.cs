namespace BotNexus.Core.Abstractions;

/// <summary>
/// A self-contained unit of scheduled work with its own schedule, type, and execution logic.
/// Implementations: AgentCronJob, SystemCronJob, MaintenanceCronJob.
/// </summary>
public interface ICronJob
{
    /// <summary>Unique job name (e.g., "morning-briefing", "memory-consolidation").</summary>
    string Name { get; }

    /// <summary>Job type discriminator: Agent, System, or Maintenance.</summary>
    CronJobType Type { get; }

    /// <summary>Cron expression (standard 5-field or 6-field with seconds).</summary>
    string Schedule { get; }

    /// <summary>Timezone for schedule evaluation. Null = UTC.</summary>
    TimeZoneInfo? TimeZone { get; }

    /// <summary>Whether this job is enabled.</summary>
    bool Enabled { get; set; }

    /// <summary>Execute the job. Returns result for tracking.</summary>
    Task<CronJobResult> ExecuteAsync(CronJobContext context, CancellationToken cancellationToken);
}
