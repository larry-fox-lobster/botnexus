namespace BotNexus.Core.Abstractions;

/// <summary>
/// Manages an agent's workspace files (SOUL.md, IDENTITY.md, USER.md, etc.)
/// and provides read/write access to workspace content.
/// </summary>
public interface IAgentWorkspace
{
    string AgentName { get; }
    string WorkspacePath { get; }

    /// <summary>Initialize workspace directory and create stub files if missing.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Read a workspace file. Returns null if file doesn't exist.</summary>
    Task<string?> ReadFileAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>Write content to a workspace file (creates if missing).</summary>
    Task WriteFileAsync(string fileName, string content, CancellationToken cancellationToken = default);

    /// <summary>Append content to a workspace file.</summary>
    Task AppendFileAsync(string fileName, string content, CancellationToken cancellationToken = default);

    /// <summary>List all files in the workspace directory.</summary>
    Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>Check if a workspace file exists.</summary>
    bool FileExists(string fileName);
}
