using System.Text.Json;
using BotNexus.AgentCore.Tools;
using BotNexus.AgentCore.Types;
using BotNexus.Providers.Core.Models;

namespace BotNexus.Cron.Tools;

public sealed class CronTool(
    ICronStore cronStore,
    CronScheduler scheduler,
    string agentId,
    bool allowCrossAgentCron = false) : IAgentTool
{
    private readonly string _agentId = string.IsNullOrWhiteSpace(agentId)
        ? throw new ArgumentException("Agent ID is required.", nameof(agentId))
        : agentId;

    public string Name => "cron";
    public string Label => "Cron Job Manager";

    public Tool Definition => new(
        Name,
        "Manage scheduled cron jobs. Create, list, update, delete, and run cron jobs.",
        JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "action": {
                  "type": "string",
                  "enum": ["list", "create", "update", "delete", "run"]
                },
                "jobId": { "type": "string", "description": "Optional - for update/delete/run." },
                "name": { "type": "string", "description": "Job name (for create)." },
                "schedule": { "type": "string", "description": "Cron expression (for create/update)." },
                "agentId": { "type": "string", "description": "Target agent (for create, defaults to calling agent)." },
                "message": { "type": "string", "description": "Prompt message (for create/update)." },
                "enabled": { "type": "boolean", "description": "Whether the job is enabled." }
              },
              "required": ["action"]
            }
            """).RootElement.Clone());

    public Task<IReadOnlyDictionary<string, object?>> PrepareArgumentsAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var action = ReadString(arguments, "action", required: true)!;
        if (!IsKnownAction(action))
            throw new ArgumentException($"Unsupported cron action '{action}'.");

        var prepared = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["action"] = action.ToLowerInvariant()
        };

        CopyString(arguments, prepared, "jobId");
        CopyString(arguments, prepared, "name");
        CopyString(arguments, prepared, "schedule");
        CopyString(arguments, prepared, "agentId");
        CopyString(arguments, prepared, "message");

        if (arguments.TryGetValue("enabled", out var enabled) && enabled is not null)
            prepared["enabled"] = ReadBool(enabled, "enabled");

        return Task.FromResult<IReadOnlyDictionary<string, object?>>(prepared);
    }

    public async Task<AgentToolResult> ExecuteAsync(
        string toolCallId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default,
        AgentToolUpdateCallback? onUpdate = null)
    {
        var action = arguments["action"]?.ToString() ?? string.Empty;
        return action switch
        {
            "list" => await ListAsync(cancellationToken).ConfigureAwait(false),
            "create" => await CreateAsync(arguments, cancellationToken).ConfigureAwait(false),
            "update" => await UpdateAsync(arguments, cancellationToken).ConfigureAwait(false),
            "delete" => await DeleteAsync(arguments, cancellationToken).ConfigureAwait(false),
            "run" => await RunAsync(arguments, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported cron action '{action}'.")
        };
    }

    private async Task<AgentToolResult> ListAsync(CancellationToken cancellationToken)
    {
        var jobs = await cronStore.ListAsync(ct: cancellationToken).ConfigureAwait(false);
        var visible = allowCrossAgentCron
            ? jobs
            : jobs.Where(job => string.Equals(job.CreatedBy, _agentId, StringComparison.OrdinalIgnoreCase)).ToList();

        return TextResult(JsonSerializer.Serialize(visible, JsonOptions));
    }

    private async Task<AgentToolResult> CreateAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var schedule = ReadRequired(arguments, "schedule");
        DateTimeOffset? nextRunAt = null;
        try
        {
            var expr = Cronos.CronExpression.Parse(schedule, Cronos.CronFormat.Standard);
            nextRunAt = expr.GetNextOccurrence(now, TimeZoneInfo.Utc);
        }
        catch { /* invalid schedule — will be caught by scheduler */ }

        var job = new CronJob
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = ReadRequired(arguments, "name"),
            Schedule = schedule,
            ActionType = "agent-prompt",
            AgentId = ReadString(arguments, "agentId") ?? _agentId,
            Message = ReadString(arguments, "message"),
            Enabled = arguments.TryGetValue("enabled", out var enabled) && enabled is bool boolEnabled ? boolEnabled : true,
            CreatedBy = _agentId,
            CreatedAt = now,
            NextRunAt = nextRunAt,
            Metadata = new Dictionary<string, object?>()
        };

        var created = await cronStore.CreateAsync(job, cancellationToken).ConfigureAwait(false);
        return TextResult(JsonSerializer.Serialize(created, JsonOptions));
    }

    private async Task<AgentToolResult> UpdateAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var jobId = ReadRequired(arguments, "jobId");
        var existing = await cronStore.GetAsync(jobId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Cron job '{jobId}' was not found.");

        EnsureCanManage(existing);

        var updated = existing with
        {
            Name = ReadString(arguments, "name") ?? existing.Name,
            Schedule = ReadString(arguments, "schedule") ?? existing.Schedule,
            Message = ReadString(arguments, "message") ?? existing.Message,
            AgentId = ReadString(arguments, "agentId") ?? existing.AgentId,
            Enabled = arguments.TryGetValue("enabled", out var enabled) && enabled is bool boolEnabled ? boolEnabled : existing.Enabled
        };

        var saved = await cronStore.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        return TextResult(JsonSerializer.Serialize(saved, JsonOptions));
    }

    private async Task<AgentToolResult> DeleteAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var jobId = ReadRequired(arguments, "jobId");
        var existing = await cronStore.GetAsync(jobId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Cron job '{jobId}' was not found.");

        EnsureCanManage(existing);
        await cronStore.DeleteAsync(jobId, cancellationToken).ConfigureAwait(false);
        return TextResult($"Deleted cron job '{jobId}'.");
    }

    private async Task<AgentToolResult> RunAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var jobId = ReadRequired(arguments, "jobId");
        var existing = await cronStore.GetAsync(jobId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Cron job '{jobId}' was not found.");

        EnsureCanManage(existing);
        var run = await scheduler.RunNowAsync(jobId, cancellationToken).ConfigureAwait(false);
        return TextResult(JsonSerializer.Serialize(run, JsonOptions));
    }

    private void EnsureCanManage(CronJob job)
    {
        if (allowCrossAgentCron)
            return;

        if (!string.Equals(job.CreatedBy, _agentId, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("You can only manage cron jobs created by this agent.");
    }

    private static AgentToolResult TextResult(string text)
        => new([new AgentToolContent(AgentToolContentType.Text, text)]);

    private static bool IsKnownAction(string action)
        => action.Equals("list", StringComparison.OrdinalIgnoreCase)
           || action.Equals("create", StringComparison.OrdinalIgnoreCase)
           || action.Equals("update", StringComparison.OrdinalIgnoreCase)
           || action.Equals("delete", StringComparison.OrdinalIgnoreCase)
           || action.Equals("run", StringComparison.OrdinalIgnoreCase);

    private static void CopyString(IReadOnlyDictionary<string, object?> source, Dictionary<string, object?> destination, string key)
    {
        var value = ReadString(source, key);
        if (!string.IsNullOrWhiteSpace(value))
            destination[key] = value;
    }

    private static string ReadRequired(IReadOnlyDictionary<string, object?> arguments, string key)
        => ReadString(arguments, key, required: true)!;

    private static string? ReadString(IReadOnlyDictionary<string, object?> arguments, string key, bool required = false)
    {
        if (!arguments.TryGetValue(key, out var value) || value is null)
        {
            if (required)
                throw new ArgumentException($"Missing required argument: {key}.");

            return null;
        }

        var result = value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement element => element.ToString(),
            _ => value.ToString()
        };

        if (required && string.IsNullOrWhiteSpace(result))
            throw new ArgumentException($"Argument '{key}' cannot be empty.");

        return result;
    }

    private static bool ReadBool(object value, string argumentName)
        => value switch
        {
            bool b => b,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.False } => false,
            JsonElement { ValueKind: JsonValueKind.String } element when bool.TryParse(element.GetString(), out var parsed) => parsed,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => throw new ArgumentException($"Argument '{argumentName}' must be a boolean.")
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
