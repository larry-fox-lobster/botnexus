using BotNexus.Core.Abstractions;
using BotNexus.Core.Models;

namespace BotNexus.Agent.Tools;

/// <summary>Central registry of executable tools for an agent.</summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a tool.</summary>
    public void Register(ITool tool) => _tools[tool.Definition.Name] = tool;

    /// <summary>Gets a tool by name.</summary>
    public ITool? Get(string name) => _tools.GetValueOrDefault(name);

    /// <summary>Returns all tool definitions (for the LLM).</summary>
    public IReadOnlyList<ToolDefinition> GetDefinitions()
        => [.. _tools.Values.Select(t => t.Definition)];

    /// <summary>Executes a tool call and returns the result.</summary>
    public async Task<string> ExecuteAsync(ToolCallRequest toolCall, CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(toolCall.ToolName, out var tool))
            return $"Error: Tool '{toolCall.ToolName}' not found.";

        try
        {
            return await tool.ExecuteAsync(toolCall.Arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return $"Error executing tool '{toolCall.ToolName}': {ex.Message}";
        }
    }
}
