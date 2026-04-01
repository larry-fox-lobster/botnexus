using System.Text.Json;
using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using BotNexus.Core.Models;
using BotNexus.Cron.Jobs;
using Microsoft.Extensions.Logging;

namespace BotNexus.Agent.Tools;

/// <summary>Tool for managing cron jobs from within an agent.</summary>
public sealed class CronTool : ToolBase
{
    private readonly ICronService? _cronService;
    private readonly IAgentRunnerFactory? _agentRunnerFactory;
    private readonly ISessionManager? _sessionManager;
    private readonly IReadOnlyList<IChannel> _channels;

    public CronTool(
        ICronService? cronService = null,
        IAgentRunnerFactory? agentRunnerFactory = null,
        ISessionManager? sessionManager = null,
        IEnumerable<IChannel>? channels = null,
        ILogger? logger = null)
        : base(logger)
    {
        _cronService = cronService;
        _agentRunnerFactory = agentRunnerFactory;
        _sessionManager = sessionManager;
        _channels = channels?.ToList() ?? [];
    }

    /// <inheritdoc/>
    public override ToolDefinition Definition => new(
        "cron",
        "Schedule or manage cron jobs. Actions: schedule, remove, list.",
        new Dictionary<string, ToolParameterSchema>
        {
            ["action"] = new("string", "Action: schedule, remove, or list", Required: true,
                EnumValues: ["schedule", "remove", "list"]),
            ["name"] = new("string", "Job name (for schedule/remove)", Required: false),
            ["schedule"] = new("string", "Cron expression (for schedule)", Required: false),
            ["expression"] = new("string", "Legacy alias for schedule (for schedule)", Required: false),
            ["agent"] = new("string", "Agent name to run (for schedule)", Required: false),
            ["prompt"] = new("string", "Prompt to execute on schedule (for schedule)", Required: false),
            ["session"] = new("string", "Session mode: new, persistent, or named:<key>", Required: false),
            ["timezone"] = new("string", "Timezone ID for schedule evaluation (optional)", Required: false),
            ["enabled"] = new("boolean", "Whether the scheduled job is enabled", Required: false),
            ["output_channels"] = new("array", "Optional channels to route agent output to", Required: false)
        });

    /// <inheritdoc/>
    protected override Task<string> ExecuteCoreAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        if (_cronService is null)
            return Task.FromResult("Error: Cron service not available");

        var action = GetOptionalString(arguments, "action", "list");

        return action.ToLowerInvariant() switch
        {
            "list" => Task.FromResult(string.Join("\n", _cronService.GetJobs().Select(j => j.Name))),
            "remove" => RemoveJob(arguments),
            "schedule" => ScheduleAgentJob(arguments),
            _ => throw new ToolArgumentException($"Unknown action '{action}'")
        };
    }

    private Task<string> ScheduleAgentJob(IReadOnlyDictionary<string, object?> args)
    {
        if (_agentRunnerFactory is null || _sessionManager is null)
            return Task.FromResult("Error: Agent cron scheduling dependencies are not available");

        var agent = GetRequiredString(args, "agent");
        var prompt = GetRequiredString(args, "prompt");
        var schedule = GetOptionalString(args, "schedule");
        if (string.IsNullOrWhiteSpace(schedule))
            schedule = GetRequiredString(args, "expression");

        var name = GetOptionalString(args, "name", $"{agent}-cron-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
        var session = GetOptionalString(args, "session");
        var timezone = GetOptionalString(args, "timezone");
        var enabled = GetOptionalBool(args, "enabled", true);
        var outputChannels = GetOptionalStringList(args, "output_channels");

        IChannel? ResolveChannel(string channelName)
            => _channels.FirstOrDefault(channel =>
                string.Equals(channel.Name, channelName, StringComparison.OrdinalIgnoreCase));

        var jobConfig = new CronJobConfig
        {
            Name = name,
            Type = "agent",
            Schedule = schedule,
            Agent = agent,
            Prompt = prompt,
            Session = string.IsNullOrWhiteSpace(session) ? null : session,
            Timezone = string.IsNullOrWhiteSpace(timezone) ? null : timezone,
            Enabled = enabled,
            OutputChannels = outputChannels
        };

        _cronService!.Register(new AgentCronJob(jobConfig, _agentRunnerFactory, _sessionManager, ResolveChannel));
        return Task.FromResult($"Agent cron job '{name}' scheduled with expression '{schedule}'");
    }

    private Task<string> RemoveJob(IReadOnlyDictionary<string, object?> args)
    {
        var name = GetRequiredString(args, "name");
        _cronService!.Remove(name);
        return Task.FromResult($"Cron job '{name}' removed");
    }

    private static List<string> GetOptionalStringList(IReadOnlyDictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var raw) || raw is null)
            return [];

        if (raw is IEnumerable<string> typed)
            return typed.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList();

        if (raw is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Array)
            {
                return json.EnumerateArray()
                    .Where(static element => element.ValueKind == JsonValueKind.String)
                    .Select(static element => element.GetString())
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value!)
                    .ToList();
            }

            return [];
        }

        if (raw is IEnumerable<object?> objects)
        {
            return objects
                .Select(static value => value?.ToString())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .ToList();
        }

        return [];
    }
}
