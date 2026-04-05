using System.Text;
using BotNexus.CodingAgent;

namespace BotNexus.CodingAgent.Utils;

public static class ContextFileDiscovery
{
    private const int ContextBudgetBytes = 16 * 1024;
    private const int MaxDocsFiles = 5;
    private const string TruncatedMarker = "[truncated]";

    public static async Task<IReadOnlyList<PromptContextFile>> DiscoverAsync(string workingDirectory, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("Working directory cannot be empty.", nameof(workingDirectory));
        }

        var root = Path.GetFullPath(workingDirectory);
        if (!Directory.Exists(root))
        {
            return [];
        }

        var discovered = new List<PromptContextFile>();
        var remainingBudget = ContextBudgetBytes;

        var prioritizedFiles = new List<string>
        {
            Path.Combine(root, ".github", "copilot-instructions.md"),
            Path.Combine(root, "README.md")
        };

        var docsDirectory = Path.Combine(root, "docs");
        if (Directory.Exists(docsDirectory))
        {
            prioritizedFiles.AddRange(Directory.EnumerateFiles(docsDirectory, "*.md", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(MaxDocsFiles));
        }

        foreach (var filePath in prioritizedFiles)
        {
            ct.ThrowIfCancellationRequested();
            if (remainingBudget <= 0 || !File.Exists(filePath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var includedContent = FitContentToBudget(content, remainingBudget);
            if (string.IsNullOrEmpty(includedContent))
            {
                break;
            }

            discovered.Add(new PromptContextFile(PathUtils.GetRelativePath(filePath, root), includedContent));
            remainingBudget -= Encoding.UTF8.GetByteCount(includedContent);
        }

        return discovered;
    }

    private static string FitContentToBudget(string content, int maxBytes)
    {
        if (maxBytes <= 0)
        {
            return string.Empty;
        }

        var fullSize = Encoding.UTF8.GetByteCount(content);
        if (fullSize <= maxBytes)
        {
            return content;
        }

        var markerBytes = Encoding.UTF8.GetByteCount(TruncatedMarker);
        if (maxBytes <= markerBytes)
        {
            return TruncatedMarker[..Math.Min(TruncatedMarker.Length, maxBytes)];
        }

        var allowedBytes = maxBytes - markerBytes;
        var builder = new StringBuilder(content.Length);
        var usedBytes = 0;
        foreach (var ch in content)
        {
            var charBytes = Encoding.UTF8.GetByteCount(ch.ToString());
            if (usedBytes + charBytes > allowedBytes)
            {
                break;
            }

            builder.Append(ch);
            usedBytes += charBytes;
        }

        builder.Append(TruncatedMarker);
        return builder.ToString();
    }
}
