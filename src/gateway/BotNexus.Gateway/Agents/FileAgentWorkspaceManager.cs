using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Configuration;

namespace BotNexus.Gateway.Agents;

public sealed class FileAgentWorkspaceManager : IAgentWorkspaceManager
{
    private readonly BotNexusHome _botNexusHome;

    public FileAgentWorkspaceManager(BotNexusHome botNexusHome)
    {
        _botNexusHome = botNexusHome;
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

        await File.AppendAllTextAsync(memoryPath, memoryEntry, cancellationToken);
    }

    public string GetWorkspacePath(string agentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        return Path.Combine(_botNexusHome.GetAgentDirectory(agentName.Trim()), "workspace");
    }

    private static async Task<string> ReadFileOrEmptyAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return string.Empty;

        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
