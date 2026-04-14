using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace BotNexus.Probe.LogIngestion;

public sealed partial class SerilogFileParser
{
    private static readonly Regex HeaderRegex = HeaderLine();

    public async IAsyncEnumerable<LogEntry> ParseDirectoryAsync(
        string directoryPath,
        LogQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            yield break;
        }

        var files = Directory.EnumerateFiles(directoryPath, "*.log*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName);

        foreach (var file in files)
        {
            await foreach (var entry in ParseFileAsync(file, query, cancellationToken))
            {
                yield return entry;
            }
        }
    }

    public async IAsyncEnumerable<LogEntry> ParseFileAsync(
        string filePath,
        LogQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            yield break;
        }

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);

        string? line;
        long lineNumber = 0;
        PendingEntry? pending = null;
        var fileDate = File.GetLastWriteTime(filePath).Date;

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;

            var headerMatch = HeaderRegex.Match(line);
            if (headerMatch.Success)
            {
                if (pending is not null)
                {
                    var built = pending.Build(filePath);
                    if (Matches(built, query))
                    {
                        yield return built;
                    }
                }

                pending = PendingEntry.FromHeader(headerMatch, fileDate, lineNumber);
                continue;
            }

            pending?.AppendDetail(line);
        }

        if (pending is not null)
        {
            var built = pending.Build(filePath);
            if (Matches(built, query))
            {
                yield return built;
            }
        }
    }

    private static bool Matches(LogEntry entry, LogQuery query)
    {
        // Time/level filters always apply (AND logic)
        if (!string.IsNullOrWhiteSpace(query.Level) && !entry.Level.Equals(query.Level, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.From is { } from && entry.Timestamp < from)
        {
            return false;
        }

        if (query.To is { } to && entry.Timestamp > to)
        {
            return false;
        }

        // AnyId: OR across all fields — correlationId, sessionId, agentId, message, exception, and every property value
        if (!string.IsNullOrWhiteSpace(query.AnyId))
        {
            if (Contains(entry.CorrelationId, query.AnyId) ||
                Contains(entry.SessionId, query.AnyId) ||
                Contains(entry.AgentId, query.AnyId) ||
                Contains(entry.Channel, query.AnyId) ||
                Contains(entry.Message, query.AnyId) ||
                Contains(entry.Exception, query.AnyId) ||
                (entry.Properties?.Any(p => Contains(p.Value, query.AnyId)) is true))
            {
                return true;
            }

            return false;
        }

        // Standard AND filters for the /api/logs endpoint
        if (!string.IsNullOrWhiteSpace(query.CorrelationId) && !Contains(entry.CorrelationId, query.CorrelationId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.SessionId) && !Contains(entry.SessionId, query.SessionId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.AgentId) && !Contains(entry.AgentId, query.AgentId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var haystack = $"{entry.Message}\n{entry.Exception}";
            if (!Contains(haystack, query.SearchText))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(string? source, string value)
        => source?.Contains(value, StringComparison.OrdinalIgnoreCase) is true;

    private static DateTimeOffset ParseTimestamp(DateTime fileDate, string value)
    {
        if (!TimeSpan.TryParse(value, out var time))
        {
            return new DateTimeOffset(fileDate, TimeZoneInfo.Local.GetUtcOffset(fileDate));
        }

        var local = fileDate.Add(time);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private static Dictionary<string, string> ParseProperties(string? rawProperties)
    {
        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawProperties))
        {
            return output;
        }

        foreach (var segment in rawProperties.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= segment.Length - 1)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();
            var value = segment[(separatorIndex + 1)..].Trim().Trim('"');
            output[key] = value;
        }

        return output;
    }

    private static string? ResolveProperty(IReadOnlyDictionary<string, string> properties, params string[] candidates)
    {
        foreach (var key in candidates)
        {
            if (properties.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts key=value pairs from inline message text.
    /// Handles formats like: "Hub SendMessage: agent=nova session=abc123 connection=xyz"
    /// </summary>
    private static Dictionary<string, string> ExtractInlineProperties(string message)
    {
        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in InlinePropertyRegex().EnumerateMatches(message.AsSpan()))
        {
            var segment = message.AsSpan(match.Index, match.Length);
            var eqIndex = segment.IndexOf('=');
            if (eqIndex > 0 && eqIndex < segment.Length - 1)
            {
                var key = segment[..eqIndex].ToString();
                var value = segment[(eqIndex + 1)..].ToString();
                output[key] = value;
            }
        }

        return output;
    }

    [GeneratedRegex(@"(?<!\w)(?:agent|session|channel|connection|correlat\w*|source|caller)=\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex InlinePropertyRegex();

    private sealed class PendingEntry(
        DateTimeOffset timestamp,
        string level,
        string message,
        Dictionary<string, string> properties,
        long lineNumber)
    {
        private readonly StringBuilder _details = new();

        public static PendingEntry FromHeader(Match headerMatch, DateTime fileDate, long lineNumber)
        {
            DateTimeOffset timestamp;
            string level;
            string message;
            Dictionary<string, string> properties;

            if (headerMatch.Groups["datetime"].Success)
            {
                // Full ISO format: 2026-04-13 22:00:06.588 -07:00 [INF] Message
                var dt = headerMatch.Groups["datetime"].Value;
                var offset = headerMatch.Groups["offset"].Value;
                timestamp = DateTimeOffset.TryParse($"{dt} {offset}", out var parsed)
                    ? parsed
                    : ParseTimestamp(fileDate, dt);
                level = headerMatch.Groups["level"].Value.Trim();
                message = headerMatch.Groups["message"].Value.Trim();
                properties = ExtractInlineProperties(message);
            }
            else
            {
                // Legacy: [HH:mm:ss LVL] Message {Properties}
                timestamp = ParseTimestamp(fileDate, headerMatch.Groups["time"].Value);
                level = headerMatch.Groups["level2"].Value.Trim();
                message = headerMatch.Groups["message2"].Value.Trim();
                properties = ParseProperties(headerMatch.Groups["props"].Success ? headerMatch.Groups["props"].Value : null);
            }

            return new PendingEntry(timestamp, level, message, properties, lineNumber);
        }

        public void AppendDetail(string line)
        {
            if (_details.Length > 0)
            {
                _details.AppendLine();
            }

            _details.Append(line);
        }

        public LogEntry Build(string sourceFile)
        {
            var exception = _details.Length > 0 ? _details.ToString() : null;
            var readOnlyProps = properties.AsReadOnly();

            return new LogEntry(
                timestamp,
                level,
                message,
                exception,
                ResolveProperty(properties, "CorrelationId", "correlationId", "correlation_id", "correlation"),
                ResolveProperty(properties, "SessionId", "sessionId", "session_id", "session"),
                ResolveProperty(properties, "AgentId", "agentId", "agent_id", "agent"),
                ResolveProperty(properties, "Channel", "ChannelType", "channel", "channelType"),
                Path.GetFileName(sourceFile),
                lineNumber,
                readOnlyProps);
        }
    }

    // Matches: 2026-04-13 22:00:06.588 -07:00 [INF] Message
    // Also matches legacy: [HH:mm:ss LVL] Message {Properties}
    [GeneratedRegex(@"^(?:(?<datetime>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(?:\.\d+)?)\s+(?<offset>[+-]\d{2}:\d{2})\s+\[(?<level>[A-Z]+)\]\s(?<message>.*)|\[(?<time>\d{2}:\d{2}:\d{2})\s+(?<level2>[A-Z]+)\]\s(?<message2>.*?)(?:\s\{(?<props>.*)\})?)$", RegexOptions.Compiled)]
    private static partial Regex HeaderLine();
}
