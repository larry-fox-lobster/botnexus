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

    public async Task<AgentWorkspace> LoadWorkspaceAsync(string agentName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        var workspacePath = GetWorkspacePath(agentName);

        var soul = await ReadFileOrEmptyAsync(Path.Combine(workspacePath, "SOUL.md"), ct);
        var identity = await ReadFileOrEmptyAsync(Path.Combine(workspacePath, "IDENTITY.md"), ct);
        var user = await ReadFileOrEmptyAsync(Path.Combine(workspacePath, "USER.md"), ct);
        var memory = await ReadFileOrEmptyAsync(Path.Combine(workspacePath, "MEMORY.md"), ct);

        return new AgentWorkspace(agentName.Trim(), soul, identity, user, memory);
    }

    public async Task SaveMemoryAsync(string agentName, string content, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var workspacePath = GetWorkspacePath(agentName);
        var memoryPath = Path.Combine(workspacePath, "MEMORY.md");
        var memoryEntry = content.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? content
            : $"{content}{Environment.NewLine}";

        await File.AppendAllTextAsync(memoryPath, memoryEntry, ct);
    }

    public string GetWorkspacePath(string agentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        return _botNexusHome.GetAgentDirectory(agentName.Trim());
    }

    private static async Task<string> ReadFileOrEmptyAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return string.Empty;

        return await File.ReadAllTextAsync(path, ct);
    }
}
