namespace BotNexus.Cron;

public sealed class CronOptions
{
    public const string SectionName = "cron";

    public bool Enabled { get; set; } = true;
    public int TickIntervalSeconds { get; set; } = 60;
}
