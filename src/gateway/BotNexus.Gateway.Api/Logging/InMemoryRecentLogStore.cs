namespace BotNexus.Gateway.Api.Logging;

public sealed class InMemoryRecentLogStore(int capacity = 1000) : IRecentLogStore
{
    private readonly Queue<RecentLogEntry> _entries = new();
    private readonly Lock _sync = new();
    private readonly int _capacity = Math.Max(capacity, 100);

    public void Add(RecentLogEntry entry)
    {
        lock (_sync)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > _capacity)
                _entries.Dequeue();
        }
    }

    public IReadOnlyList<RecentLogEntry> GetRecent(int limit)
    {
        var count = Math.Clamp(limit, 1, 500);
        lock (_sync)
        {
            return _entries
                .Reverse()
                .Take(count)
                .ToArray();
        }
    }
}
