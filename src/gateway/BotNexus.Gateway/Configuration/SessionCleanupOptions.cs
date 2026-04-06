namespace BotNexus.Gateway.Configuration;

public sealed class SessionCleanupOptions
{
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan? ClosedSessionRetention { get; set; }
}
