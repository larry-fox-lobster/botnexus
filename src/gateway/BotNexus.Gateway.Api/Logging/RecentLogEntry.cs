namespace BotNexus.Gateway.Api.Logging;

public sealed record RecentLogEntry(
    DateTimeOffset Timestamp,
    string Category,
    string Level,
    string Message,
    string? Exception,
    IReadOnlyDictionary<string, object?> Properties);
