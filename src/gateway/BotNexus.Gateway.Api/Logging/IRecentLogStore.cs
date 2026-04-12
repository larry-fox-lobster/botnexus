namespace BotNexus.Gateway.Api.Logging;

public interface IRecentLogStore
{
    void Add(RecentLogEntry entry);

    IReadOnlyList<RecentLogEntry> GetRecent(int limit);
}
