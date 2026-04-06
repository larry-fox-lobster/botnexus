namespace BotNexus.Gateway.Abstractions.Agents;

public interface IAgentWorkspaceManager
{
    Task<AgentWorkspace> LoadWorkspaceAsync(string agentName, CancellationToken ct = default);
    Task SaveMemoryAsync(string agentName, string content, CancellationToken ct = default);
    string GetWorkspacePath(string agentName);
}
