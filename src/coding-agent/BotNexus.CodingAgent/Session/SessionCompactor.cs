using System.Text.RegularExpressions;
using BotNexus.AgentCore.Types;

namespace BotNexus.CodingAgent.Session;

/// <summary>
/// Compacts conversation context when token limits are approached.
/// Keeps recent messages intact and summarizes older ones.
/// </summary>
public class SessionCompactor
{
    /// <summary>
    /// Compact messages to fit within token budget.
    /// </summary>
    /// <param name="messages">Current message history</param>
    /// <param name="keepRecentCount">Number of recent messages to keep intact (default: 10)</param>
    /// <returns>Compacted message list with summary replacing old messages</returns>
    public IReadOnlyList<AgentMessage> Compact(
        IReadOnlyList<AgentMessage> messages,
        int keepRecentCount = 10)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (keepRecentCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(keepRecentCount), "keepRecentCount must be >= 0.");
        }

        if (messages.Count <= keepRecentCount)
        {
            return messages;
        }

        var oldMessages = messages.Take(messages.Count - keepRecentCount).ToList();
        var recentMessages = messages.Skip(messages.Count - keepRecentCount).ToList();

        var keyTopics = ExtractKeyTopics(oldMessages);
        var filesModified = ExtractFilePaths(oldMessages);
        var decisionsMade = ExtractDecisions(oldMessages);

        var summaryText =
            $"[Session context summary: {oldMessages.Count} earlier messages compacted.{Environment.NewLine}" +
            $" Key topics discussed: {string.Join(", ", keyTopics)}{Environment.NewLine}" +
            $" Files modified: {string.Join(", ", filesModified)}{Environment.NewLine}" +
            $" Decisions made: {string.Join("; ", decisionsMade)}]";

        var compacted = new List<AgentMessage>(1 + recentMessages.Count)
        {
            new SystemAgentMessage(summaryText)
        };
        compacted.AddRange(recentMessages);
        return compacted;
    }

    private static IReadOnlyList<string> ExtractKeyTopics(IReadOnlyList<AgentMessage> messages)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "that", "with", "this", "from", "have", "will", "were", "into", "tool", "message", "messages", "file"
        };

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var text in ExtractText(messages))
        {
            foreach (var word in Regex.Matches(text, "[A-Za-z][A-Za-z0-9_-]{3,}").Select(match => match.Value))
            {
                if (stopWords.Contains(word))
                {
                    continue;
                }

                counts[word] = counts.TryGetValue(word, out var current) ? current + 1 : 1;
            }
        }

        return counts.Count == 0
            ? ["none identified"]
            : counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Take(5).Select(pair => pair.Key).ToList();
    }

    private static IReadOnlyList<string> ExtractFilePaths(IReadOnlyList<AgentMessage> messages)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var toolResult in messages.OfType<ToolResultAgentMessage>())
        {
            foreach (var content in toolResult.Result.Content)
            {
                foreach (Match match in Regex.Matches(content.Value, @"(?<!\w)([A-Za-z0-9_\-./\\]+?\.[A-Za-z0-9]{1,8})(?::\d+)?"))
                {
                    results.Add(match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar));
                    if (results.Count >= 5)
                    {
                        return results.ToList();
                    }
                }
            }
        }

        return results.Count == 0 ? ["none identified"] : results.ToList();
    }

    private static IReadOnlyList<string> ExtractDecisions(IReadOnlyList<AgentMessage> messages)
    {
        var decisions = new List<string>();
        foreach (var text in ExtractText(messages))
        {
            if (!Regex.IsMatch(text, @"\b(decide|decision|should|will|use|implement|add|fix)\b", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var cleaned = Regex.Replace(text, @"\s+", " ").Trim();
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                decisions.Add(cleaned.Length > 120 ? $"{cleaned[..117]}..." : cleaned);
            }

            if (decisions.Count >= 3)
            {
                break;
            }
        }

        return decisions.Count == 0 ? ["none identified"] : decisions;
    }

    private static IEnumerable<string> ExtractText(IReadOnlyList<AgentMessage> messages)
    {
        foreach (var message in messages)
        {
            switch (message)
            {
                case UserMessage user:
                    if (!string.IsNullOrWhiteSpace(user.Content))
                    {
                        yield return user.Content;
                    }

                    break;
                case AssistantAgentMessage assistant:
                    if (!string.IsNullOrWhiteSpace(assistant.Content))
                    {
                        yield return assistant.Content;
                    }

                    break;
                case SystemAgentMessage system:
                    if (!string.IsNullOrWhiteSpace(system.Content))
                    {
                        yield return system.Content;
                    }

                    break;
                case ToolResultAgentMessage tool:
                    foreach (var content in tool.Result.Content)
                    {
                        if (!string.IsNullOrWhiteSpace(content.Value))
                        {
                            yield return content.Value;
                        }
                    }

                    break;
            }
        }
    }
}
