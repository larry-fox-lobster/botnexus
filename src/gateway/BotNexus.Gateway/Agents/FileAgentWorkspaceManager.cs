using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Configuration;
using System.IO.Abstractions;

namespace BotNexus.Gateway.Agents;

public sealed class FileAgentWorkspaceManager : IAgentWorkspaceManager
{
    private readonly BotNexusHome _botNexusHome;
    private readonly IFileSystem _fileSystem;

    public FileAgentWorkspaceManager(BotNexusHome botNexusHome, IFileSystem fileSystem)
    {
        _botNexusHome = botNexusHome;
        _fileSystem = fileSystem;
    }

    public async Task<AgentWorkspace> LoadWorkspaceAsync(string agentName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        var workspacePath = GetWorkspacePath(agentName);

        var soul = await ReadFileOrEmptyAsync(Path.Combine(workspacePath, "SOUL.md"), cancellationToken);
        var identity = await ReadFileOrEmptyAsync(Path.Combine(workspacePath, "IDENTITY.md"), cancellationToken);
        var user = await ReadFileOrEmptyAsync(Path.Combine(workspacePath, "USER.md"), cancellationToken);
        var memory = await ReadFileOrEmptyAsync(Path.Combine(workspacePath, "MEMORY.md"), cancellationToken);

        return new AgentWorkspace(agentName.Trim(), soul, identity, user, memory);
    }

    public async Task SaveMemoryAsync(string agentName, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var workspacePath = GetWorkspacePath(agentName);
        var memoryPath = Path.Combine(workspacePath, "MEMORY.md");
        var memoryEntry = content.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? content
            : $"{content}{Environment.NewLine}";

        await _fileSystem.File.AppendAllTextAsync(memoryPath, memoryEntry, cancellationToken);
    }

    public string GetWorkspacePath(string agentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        return Path.Combine(_botNexusHome.GetAgentDirectory(agentName.Trim()), "workspace");
    }

    private async Task<string> ReadFileOrEmptyAsync(string path, CancellationToken cancellationToken)
    {
        if (!_fileSystem.File.Exists(path))
            return string.Empty;

        return await _fileSystem.File.ReadAllTextAsync(path, cancellationToken);
    }
}
