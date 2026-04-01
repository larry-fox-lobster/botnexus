using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using Microsoft.Extensions.Options;

namespace BotNexus.Cron;

public sealed class CronHeartbeatAdapter(ICronService cronService, IOptions<CronConfig> options) : IHeartbeatService
{
    private readonly ICronService _cronService = cronService ?? throw new ArgumentNullException(nameof(cronService));
    private readonly CronConfig _cronConfig = options?.Value ?? new CronConfig();
    private DateTimeOffset? _manualBeat;

    public void Beat() => _manualBeat = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastBeat
    {
        get
        {
            var latestCronBeat = _cronService.GetJobs()
                .Select(static job => job.LastRun)
                .Where(static timestamp => timestamp.HasValue)
                .Select(static timestamp => timestamp!.Value)
                .OrderByDescending(static timestamp => timestamp)
                .FirstOrDefault();

            if (_manualBeat is null)
                return latestCronBeat == default ? null : latestCronBeat;

            if (latestCronBeat == default)
                return _manualBeat;

            return _manualBeat.Value > latestCronBeat ? _manualBeat : latestCronBeat;
        }
    }

    public bool IsHealthy
    {
        get
        {
            if (!_cronConfig.Enabled)
                return true;

            var lastBeat = LastBeat;
            if (!lastBeat.HasValue)
                return _cronService.GetJobs().Count == 0;

            var tickSeconds = _cronConfig.TickIntervalSeconds > 0 ? _cronConfig.TickIntervalSeconds : 10;
            return DateTimeOffset.UtcNow - lastBeat.Value <= TimeSpan.FromSeconds(tickSeconds * 3);
        }
    }
}
