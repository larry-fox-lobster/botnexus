using BotNexus.Core.Abstractions;

namespace BotNexus.Heartbeat;

/// <summary>Compatibility shim that delegates heartbeat health checks to the cron service.</summary>
public sealed class HeartbeatService(ICronService cronService) : IHeartbeatService
{
    private readonly ICronService _cronService = cronService ?? throw new ArgumentNullException(nameof(cronService));

    /// <inheritdoc/>
    public void Beat() { }

    /// <inheritdoc/>
    public DateTimeOffset? LastBeat => _cronService.GetJobs()
        .Where(static job => job.LastRun.HasValue)
        .Select(static job => job.LastRun)
        .Max();

    /// <inheritdoc/>
    public bool IsHealthy => _cronService.IsRunning;
}
