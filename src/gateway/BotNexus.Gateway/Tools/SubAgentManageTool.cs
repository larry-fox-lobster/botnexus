using System.Text.Json;
using BotNexus.AgentCore.Tools;
using BotNexus.AgentCore.Types;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Providers.Core.Models;

namespace BotNexus.Gateway.Tools;

public sealed class SubAgentManageTool(
    ISubAgentManager manager,
    string sessionId) : IAgentTool
{
    public string Name => "manage_subagent";
    public string Label => "Manage Sub-Agent";

    public Tool Definition => new(
        Name,
        "Get status or terminate a running sub-agent.",
        JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "subAgentId": {
                  "type": "string",
                  "description": "The sub-agent to manage"
                },
                "action": {
                  "type": "string",
                  "enum": ["status", "kill"],
                  "description": "Action to perform"
                }
              },
              "required": ["subAgentId", "action"]
            }
            """).RootElement.Clone());

    public Task<IReadOnlyDictionary<string, object?>> PrepareArgumentsAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var subAgentId = ReadString(arguments, "subAgentId");
        if (string.IsNullOrWhiteSpace(subAgentId))
            throw new ArgumentException("Missing required argument: subAgentId.");

        var action = ReadString(arguments, "action");
        if (!string.Equals(action, "status", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(action, "kill", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid argument: action must be 'status' or 'kill'.");

        return Task.FromResult(arguments);
    }

    public async Task<AgentToolResult> ExecuteAsync(
        string toolCallId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default,
        AgentToolUpdateCallback? onUpdate = null)
    {
        var subAgentId = ReadString(arguments, "subAgentId")!.Trim();
        var action = ReadString(arguments, "action")!.ToLowerInvariant();

        if (action == "status")
        {
            var info = await manager.GetAsync(subAgentId, cancellationToken)
                ?? throw new KeyNotFoundException($"Sub-agent '{subAgentId}' not found.");
            return TextResult(JsonSerializer.Serialize(info, JsonOptions));
        }

        var killed = await manager.KillAsync(subAgentId, sessionId, cancellationToken);
        var result = new
        {
            SubAgentId = subAgentId,
            Success = killed
        };

        return TextResult(JsonSerializer.Serialize(result, JsonOptions));
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var value) || value is null)
            return null;

        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } el => el.GetString(),
            JsonElement el => el.ToString(),
            _ => value.ToString()
        };
    }

    private static AgentToolResult TextResult(string text)
        => new([new AgentToolContent(AgentToolContentType.Text, text)]);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
