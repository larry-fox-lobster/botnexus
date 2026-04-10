using System.Text.Json;
using BotNexus.AgentCore.Tools;
using BotNexus.AgentCore.Types;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Providers.Core.Models;

namespace BotNexus.Gateway.Tools;

public sealed class SubAgentListTool(
    ISubAgentManager manager,
    string sessionId) : IAgentTool
{
    public string Name => "list_subagents";
    public string Label => "List Sub-Agents";

    public Tool Definition => new(
        Name,
        "List active sub-agents spawned by this session.",
        JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {}
            }
            """).RootElement.Clone());

    public Task<IReadOnlyDictionary<string, object?>> PrepareArgumentsAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(arguments);
    }

    public async Task<AgentToolResult> ExecuteAsync(
        string toolCallId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default,
        AgentToolUpdateCallback? onUpdate = null)
    {
        var subAgents = await manager.ListAsync(sessionId, cancellationToken);
        var summaries = subAgents.Select(info => new
        {
            info.SubAgentId,
            info.Name,
            info.Status,
            info.Model,
            info.StartedAt,
            info.TurnsUsed,
            TaskPreview = Truncate(info.Task, 200)
        });

        return TextResult(JsonSerializer.Serialize(summaries, JsonOptions));
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";

    private static AgentToolResult TextResult(string text)
        => new([new AgentToolContent(AgentToolContentType.Text, text)]);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
