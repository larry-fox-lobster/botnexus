using BotNexus.Probe.LogIngestion;
using BotNexus.Probe.Otel;
using System.Text;

namespace BotNexus.Probe.Cli;

public static class CorrelateCommand
{
    public static async Task<int> RunAsync(
        CliOptions options,
        string[] args,
        SerilogFileParser logParser,
        JsonlSessionReader sessionReader,
        SessionDbReader? sessionDbReader,
        TraceStore traceStore,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            CliOutput.WriteError("correlate command requires an id. Usage: probe correlate <id> [--take N]");
            return 1;
        }

        var id = args[0];
        var take = ParseTake(args[1..]);
        var logs = new List<LogEntry>();
        var sessions = new List<object>();

        // Search logs with OR logic across all fields
        var logQuery = new LogQuery(AnyId: id);

        await foreach (var logEntry in logParser.ParseDirectoryAsync(options.LogsPath, logQuery, cancellationToken))
        {
            logs.Add(logEntry);
            if (logs.Count >= take)
            {
                break;
            }
        }

        // Search JSONL session files
        if (Directory.Exists(options.SessionsPath))
        {
            foreach (var sessionFile in Directory.EnumerateFiles(options.SessionsPath, "*.jsonl", SearchOption.TopDirectoryOnly))
            {
                await foreach (var message in sessionReader.ReadMessagesAsync(sessionFile, cancellationToken: cancellationToken))
                {
                    if (!IsMatch(message, id))
                    {
                        continue;
                    }

                    sessions.Add(new
                    {
                        source = "jsonl",
                        sessionId = message.SessionId,
                        role = message.Role,
                        content = message.Content,
                        timestamp = message.Timestamp,
                        agentId = message.AgentId
                    });
                    if (sessions.Count >= take)
                    {
                        break;
                    }
                }

                if (sessions.Count >= take)
                {
                    break;
                }
            }
        }

        // Search SQLite DB — session by ID, history by session ID, and content search
        if (sessionDbReader is not null)
        {
            try
            {
                var detail = await sessionDbReader.GetSessionAsync(id, cancellationToken);
                if (detail is not null)
                {
                    sessions.Add(new
                    {
                        source = "sqlite",
                        match = "sessions.id",
                        sessionId = detail.Id,
                        agentId = detail.AgentId,
                        channelType = detail.ChannelType,
                        sessionType = detail.SessionType,
                        status = detail.Status,
                        createdAt = detail.CreatedAt
                    });
                }

                var remaining = take - sessions.Count;

                if (remaining > 0)
                {
                    var bySessionId = await sessionDbReader.GetHistoryAsync(id, take: Math.Min(remaining, 100), ct: cancellationToken);
                    foreach (var entry in bySessionId)
                    {
                        sessions.Add(new
                        {
                            source = "sqlite",
                            match = "session_history.session_id",
                            sessionId = entry.SessionId,
                            role = entry.Role,
                            content = entry.Content,
                            timestamp = entry.Timestamp,
                            toolName = entry.ToolName
                        });
                    }
                }

                remaining = take - sessions.Count;

                if (remaining > 0)
                {
                    var byContent = await sessionDbReader.SearchHistoryAsync(id, null, Math.Min(remaining, 100), cancellationToken);
                    foreach (var entry in byContent)
                    {
                        if (entry.SessionId == id)
                        {
                            continue;
                        }

                        sessions.Add(new
                        {
                            source = "sqlite",
                            match = "session_history.content",
                            sessionId = entry.SessionId,
                            role = entry.Role,
                            content = entry.Content,
                            timestamp = entry.Timestamp,
                            toolName = entry.ToolName
                        });
                    }
                }
            }
            catch
            {
                // Best effort only.
            }
        }

        var traceMatches = traceStore.GetTraces(10_000)
            .Where(span =>
                span.TraceId.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                span.SpanId.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                span.Attributes.Any(attribute => attribute.Value.Contains(id, StringComparison.OrdinalIgnoreCase)))
            .Take(take)
            .ToList();

        var hasResults = logs.Count > 0 || sessions.Count > 0 || traceMatches.Count > 0;
        var payload = new
        {
            status = hasResults ? "ok" : "empty",
            id,
            logs = new { count = logs.Count, items = logs },
            sessions = new { count = sessions.Count, items = sessions },
            traces = new { enabled = true, count = traceMatches.Count, items = traceMatches }
        };

        CliOutput.Write(options, payload, () => FormatText(id, logs, sessions, traceMatches));
        return hasResults ? 0 : 2;
    }

    private static string FormatText(
        string id,
        IReadOnlyList<LogEntry> logs,
        IReadOnlyList<object> sessions,
        IReadOnlyList<SpanModel> traces)
    {
        var output = new StringBuilder();
        output.AppendLine($"🔎 Correlation: {id}");
        output.AppendLine("━━━━━━━━━━━━━━━━━━━━━━");
        output.AppendLine($"📄 Logs: {logs.Count} entries found");
        output.AppendLine($"💬 Sessions: {sessions.Count} items matched");
        output.AppendLine($"🔗 Traces: {traces.Count} spans found");

        if (logs.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("--- Log Entries ---");
            foreach (var entry in logs)
            {
                output.AppendLine($"[{entry.Timestamp.ToLocalTime():HH:mm:ss} {entry.Level}] {entry.Message}");
            }
        }

        if (sessions.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("--- Session Data ---");
            foreach (var item in sessions)
            {
                output.AppendLine($"  {item}");
            }
        }

        if (traces.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("--- Traces ---");
            foreach (var trace in traces)
            {
                output.AppendLine($"[{trace.StartTime.ToLocalTime():HH:mm:ss}] {trace.ServiceName}/{trace.OperationName} ({trace.TraceId})");
            }
        }

        return output.ToString().TrimEnd();
    }

    private static int ParseTake(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].Equals("--take", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 < args.Length && int.TryParse(args[index + 1], out var parsed))
            {
                return Math.Clamp(parsed, 1, 1_000);
            }
        }

        return 250;
    }

    private static bool IsMatch(SessionMessage message, string id)
    {
        if (message.SessionId.Contains(id, StringComparison.OrdinalIgnoreCase) ||
            Contains(message.AgentId, id) ||
            Contains(message.Content, id))
        {
            return true;
        }

        return message.Metadata?.Any(pair =>
            pair.Key.Contains(id, StringComparison.OrdinalIgnoreCase) ||
            pair.Value.ToString().Contains(id, StringComparison.OrdinalIgnoreCase)) is true;
    }

    private static bool Contains(string? source, string value)
        => source?.Contains(value, StringComparison.OrdinalIgnoreCase) is true;
}
