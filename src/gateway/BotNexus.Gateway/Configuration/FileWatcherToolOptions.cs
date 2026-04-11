namespace BotNexus.Gateway.Configuration;

public sealed class FileWatcherToolOptions
{
    public int MaxTimeoutSeconds { get; set; } = 1800; // 30 minutes
    public int DefaultTimeoutSeconds { get; set; } = 300; // 5 minutes
    public int DebounceMilliseconds { get; set; } = 500;
}
