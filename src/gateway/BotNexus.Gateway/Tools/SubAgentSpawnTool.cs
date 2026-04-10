using System.Text.Json;
using BotNexus.AgentCore.Tools;
using BotNexus.AgentCore.Types;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Providers.Core.Models;

namespace BotNexus.Gateway.Tools;

public sealed class SubAgentSpawnTool(
    ISubAgentManager manager,
    string agentId,
    string sessionId) : IAgentTool
{
    public string Name => "spawn_subagent";
    public string Label => "Spawn Sub-Agent";

    public Tool Definition => new(
        Name,
        "Spawn a background sub-agent to perform work independently and report results back.",
        JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "task": {
                  "type": "string",
                  "description": "Task description / initial prompt for the sub-agent"
                },
                "name": {
                  "type": "string",
                  "description": "Human-readable label"
                },
                "model": {
                  "type": "string",
                  "description": "Model override"
                },
                "tools": {
                  "type": "array",
                  "items": { "type": "string" },
                  "description": "Tool allowlist"
                },
                "systemPrompt": {
                  "type": "string",
                  "description": "Additional system prompt"
                },
                "maxTurns": {
                  "type": "integer",
                  "description": "Max turns before auto-stop"
                },
                "timeout": {
                  "type": "integer",
                  "description": "Timeout in seconds"
                }
              },
              "required": ["task"]
            }
            """).RootElement.Clone());

    public Task<IReadOnlyDictionary<string, object?>> PrepareArgumentsAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var task = ReadString(arguments, "task");
        if (string.IsNullOrWhiteSpace(task))
            throw new ArgumentException("Missing required argument: task.");

        return Task.FromResult(arguments);
    }

    public async Task<AgentToolResult> ExecuteAsync(
        string toolCallId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default,
        AgentToolUpdateCallback? onUpdate = null)
    {
        var request = new SubAgentSpawnRequest
        {
            ParentAgentId = agentId,
            ParentSessionId = sessionId,
            Task = ReadString(arguments, "task")!.Trim(),
            Name = ReadString(arguments, "name"),
            ModelOverride = ReadString(arguments, "model"),
            ToolIds = ReadStringArray(arguments, "tools"),
            SystemPromptOverride = ReadString(arguments, "systemPrompt"),
            MaxTurns = ReadInt(arguments, "maxTurns", 30),
            TimeoutSeconds = ReadInt(arguments, "timeout", 600)
        };

        var info = await manager.SpawnAsync(request, cancellationToken);

        var result = new
        {
            info.SubAgentId,
            SessionId = info.ChildSessionId,
            info.Status,
            info.Name
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

    private static int ReadInt(IReadOnlyDictionary<string, object?> args, string key, int defaultValue)
    {
        if (!args.TryGetValue(key, out var value) || value is null)
            return defaultValue;

        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.Number } el when el.TryGetInt32(out var i) => i,
            JsonElement { ValueKind: JsonValueKind.String } el when int.TryParse(el.GetString(), out var i) => i,
            int i => i,
            string s when int.TryParse(s, out var i) => i,
            _ => defaultValue
        };
    }

    private static IReadOnlyList<string>? ReadStringArray(IReadOnlyDictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var value) || value is null)
            return null;

        if (value is JsonElement { ValueKind: JsonValueKind.Array } element)
        {
            var items = element
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToArray();

            return items.Length == 0 ? null : items;
        }

        if (value is IEnumerable<string> enumerable)
        {
            var items = enumerable.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
            return items.Length == 0 ? null : items;
        }

        return null;
    }

    private static AgentToolResult TextResult(string text)
        => new([new AgentToolContent(AgentToolContentType.Text, text)]);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
