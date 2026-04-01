using BotNexus.Core.Abstractions;
using BotNexus.Core.Models;

namespace BotNexus.Agent.Tools;

/// <summary>Tool for managing cron jobs from within an agent.</summary>
public sealed class CronTool : ITool
{
    private readonly ICronService? _cronService;

    public CronTool(ICronService? cronService = null)
    {
        _cronService = cronService;
    }

    /// <inheritdoc/>
    public ToolDefinition Definition => new(
        "cron",
        "Schedule or manage cron jobs. Actions: schedule, remove, list.",
        new Dictionary<string, ToolParameterSchema>
        {
            ["action"] = new("string", "Action: schedule, remove, or list", Required: true,
                EnumValues: ["schedule", "remove", "list"]),
            ["name"] = new("string", "Job name (for schedule/remove)", Required: false),
            ["expression"] = new("string", "Cron expression (for schedule)", Required: false),
            ["message"] = new("string", "Message payload (for schedule)", Required: false)
        });

    /// <inheritdoc/>
    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        if (_cronService is null)
            return Task.FromResult("Error: Cron service not available");

        var action = arguments.GetValueOrDefault("action")?.ToString() ?? "list";

        return action.ToLowerInvariant() switch
        {
            "list" => Task.FromResult(string.Join("\n", _cronService.GetScheduledJobs())),
            "remove" => RemoveJob(arguments),
            "schedule" => ScheduleJob(arguments),
            _ => Task.FromResult($"Error: Unknown action '{action}'")
        };
    }

    private Task<string> ScheduleJob(IReadOnlyDictionary<string, object?> args)
    {
        var name = args.GetValueOrDefault("name")?.ToString();
        var expression = args.GetValueOrDefault("expression")?.ToString();
        var message = args.GetValueOrDefault("message")?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(name)) return Task.FromResult("Error: name is required");
        if (string.IsNullOrEmpty(expression)) return Task.FromResult("Error: expression is required");

        _cronService!.Schedule(name, expression, ct =>
        {
            // In a real implementation, this would publish to the message bus
            return Task.CompletedTask;
        });

        return Task.FromResult($"Cron job '{name}' scheduled with expression '{expression}'");
    }

    private Task<string> RemoveJob(IReadOnlyDictionary<string, object?> args)
    {
        var name = args.GetValueOrDefault("name")?.ToString();
        if (string.IsNullOrEmpty(name)) return Task.FromResult("Error: name is required");
        _cronService!.Remove(name);
        return Task.FromResult($"Cron job '{name}' removed");
    }
}
