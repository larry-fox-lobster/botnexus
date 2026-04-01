using BotNexus.Core.Abstractions;
using BotNexus.Core.Models;

namespace BotNexus.Agent.Tools;

/// <summary>Tool for reading and writing files in the agent workspace.</summary>
public sealed class FilesystemTool : ITool
{
    private readonly string _workspacePath;
    private readonly bool _restrictToWorkspace;

    public FilesystemTool(string workspacePath, bool restrictToWorkspace = true)
    {
        _workspacePath = workspacePath;
        _restrictToWorkspace = restrictToWorkspace;
    }

    /// <inheritdoc/>
    public ToolDefinition Definition => new(
        "filesystem",
        "Read, write, list, or delete files. Use action='read', 'write', 'list', or 'delete'.",
        new Dictionary<string, ToolParameterSchema>
        {
            ["action"] = new("string", "Action: read, write, list, or delete", Required: true,
                EnumValues: ["read", "write", "list", "delete"]),
            ["path"] = new("string", "File or directory path", Required: true),
            ["content"] = new("string", "Content to write (for write action)", Required: false)
        });

    /// <inheritdoc/>
    public async Task<string> ExecuteAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var action = arguments.GetValueOrDefault("action")?.ToString() ?? "read";
        var path = arguments.GetValueOrDefault("path")?.ToString() ?? string.Empty;
        var resolvedPath = ResolvePath(path);

        if (_restrictToWorkspace && !resolvedPath.StartsWith(_workspacePath, StringComparison.OrdinalIgnoreCase))
            return $"Error: Access denied. Path must be within workspace: {_workspacePath}";

        return action.ToLowerInvariant() switch
        {
            "read" => await ReadFileAsync(resolvedPath, cancellationToken),
            "write" => await WriteFileAsync(resolvedPath, arguments.GetValueOrDefault("content")?.ToString() ?? string.Empty, cancellationToken),
            "list" => ListDirectory(resolvedPath),
            "delete" => DeleteFile(resolvedPath),
            _ => $"Error: Unknown action '{action}'"
        };
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(_workspacePath, path));
    }

    private static async Task<string> ReadFileAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return $"Error: File not found: {path}";
        return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
    }

    private static async Task<string> WriteFileAsync(string path, string content, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, ct).ConfigureAwait(false);
        return $"Written {content.Length} bytes to {path}";
    }

    private static string ListDirectory(string path)
    {
        if (!Directory.Exists(path)) return $"Error: Directory not found: {path}";
        var entries = Directory.GetFileSystemEntries(path);
        return string.Join("\n", entries);
    }

    private static string DeleteFile(string path)
    {
        if (File.Exists(path)) { File.Delete(path); return $"Deleted: {path}"; }
        if (Directory.Exists(path)) { Directory.Delete(path, recursive: true); return $"Deleted directory: {path}"; }
        return $"Error: Path not found: {path}";
    }
}
