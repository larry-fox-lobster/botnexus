using System.Text.Json;
using BotNexus.AgentCore.Tools;
using BotNexus.AgentCore.Types;
using BotNexus.CodingAgent.Utils;
using BotNexus.Providers.Core.Models;

namespace BotNexus.CodingAgent.Tools;

public sealed class ListDirectoryTool : IAgentTool
{
    private const int DefaultLimit = 500;
    private const int MaxOutputBytes = 50 * 1024;
    private readonly string _workingDirectory;

    public ListDirectoryTool(string workingDirectory)
    {
        _workingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? throw new ArgumentException("Working directory cannot be empty.", nameof(workingDirectory))
            : Path.GetFullPath(workingDirectory);
    }

    public string Name => "ls";
    public string Label => "List Directory";

    public Tool Definition => new(
        Name,
        "List directory entries in a flat sorted listing.",
        JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "path": { "type": "string" },
                "limit": { "type": "integer" }
              }
            }
            """).RootElement.Clone());

    public Task<IReadOnlyDictionary<string, object?>> PrepareArgumentsAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (arguments.TryGetValue("path", out var pathObj) && pathObj is not null)
        {
            prepared["path"] = ReadRequiredString(arguments, "path");
        }

        if (arguments.TryGetValue("limit", out var limitObj) && limitObj is not null)
        {
            var limit = ReadInt(limitObj, "limit");
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arguments), "limit must be greater than 0.");
            }

            prepared["limit"] = limit;
        }

        return Task.FromResult<IReadOnlyDictionary<string, object?>>(prepared);
    }

    public Task<AgentToolResult> ExecuteAsync(string toolCallId, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default, AgentToolUpdateCallback? onUpdate = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rawPath = arguments.TryGetValue("path", out var pathObj) && pathObj is not null
            ? pathObj.ToString()!
            : ".";
        var limit = arguments.TryGetValue("limit", out var limitObj) && limitObj is int parsedLimit
            ? parsedLimit
            : DefaultLimit;

        var resolvedPath = PathUtils.ResolvePath(rawPath, _workingDirectory);
        if (!Directory.Exists(resolvedPath))
        {
            return Task.FromResult(new AgentToolResult([new AgentToolContent(AgentToolContentType.Text, $"Path '{rawPath}' does not exist or is not a directory.")]));
        }

        var entries = Directory.EnumerateFileSystemEntries(resolvedPath, "*", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (entries.Count == 0)
        {
            return Task.FromResult(new AgentToolResult([new AgentToolContent(AgentToolContentType.Text, "(empty directory)")]));
        }

        var outputLines = new List<string>();
        var outputBytes = 0;
        var entryLimitReached = false;
        var byteLimitReached = false;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (outputLines.Count >= limit)
            {
                entryLimitReached = true;
                break;
            }

            var fullPath = Path.Combine(resolvedPath, entry);
            var formatted = Directory.Exists(fullPath) ? $"{entry}/" : entry;
            var lineBytes = System.Text.Encoding.UTF8.GetByteCount(formatted + Environment.NewLine);
            if (outputBytes + lineBytes > MaxOutputBytes)
            {
                byteLimitReached = true;
                break;
            }

            outputLines.Add(formatted);
            outputBytes += lineBytes;
        }

        var output = string.Join(Environment.NewLine, outputLines);
        var notices = new List<string>();
        if (entryLimitReached)
        {
            notices.Add($"{limit} entries limit reached");
        }

        if (byteLimitReached)
        {
            notices.Add($"{MaxOutputBytes} byte limit reached");
        }

        if (notices.Count > 0)
        {
            output = $"{output}{Environment.NewLine}{Environment.NewLine}[{string.Join(". ", notices)}]";
        }

        return Task.FromResult(new AgentToolResult([new AgentToolContent(AgentToolContentType.Text, output)]));
    }

    private static string ReadRequiredString(IReadOnlyDictionary<string, object?> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value) || value is null)
        {
            throw new ArgumentException($"Missing required argument: {key}.");
        }

        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? throw new ArgumentException($"Argument '{key}' cannot be null."),
            JsonElement element => element.ToString(),
            _ => value.ToString() ?? throw new ArgumentException($"Argument '{key}' is invalid.")
        };
    }

    private static int ReadInt(object value, string key)
    {
        return value switch
        {
            int i => i,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt32(out var parsedInt) => parsedInt,
            JsonElement { ValueKind: JsonValueKind.String } element when int.TryParse(element.GetString(), out var parsedText) => parsedText,
            string text when int.TryParse(text, out var parsedText) => parsedText,
            _ => throw new ArgumentException($"Argument '{key}' must be an integer.")
        };
    }

}
