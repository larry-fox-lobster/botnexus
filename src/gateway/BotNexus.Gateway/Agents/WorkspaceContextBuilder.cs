using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Agents;

public sealed class WorkspaceContextBuilder : IContextBuilder
{
    private const string SectionSeparator = "\n\n---\n\n";
    private readonly IAgentWorkspaceManager _workspaceManager;

    public WorkspaceContextBuilder(IAgentWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    public async Task<string> BuildSystemPromptAsync(AgentDescriptor descriptor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var workspace = await _workspaceManager.LoadWorkspaceAsync(descriptor.AgentId, ct);
        if (string.IsNullOrWhiteSpace(workspace.Soul))
            return descriptor.SystemPrompt ?? string.Empty;

        var sections = new[]
        {
            workspace.Soul,
            workspace.Identity,
            descriptor.SystemPrompt,
            workspace.User
        }
        .Where(static value => !string.IsNullOrWhiteSpace(value))
        .Select(static value => value!.Trim());

        return string.Join(SectionSeparator, sections);
    }
}
