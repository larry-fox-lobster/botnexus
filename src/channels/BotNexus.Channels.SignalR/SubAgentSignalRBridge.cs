using BotNexus.Gateway.Abstractions.Activity;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Domain.Primitives;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BotNexus.Channels.SignalR;

/// <summary>
/// Bridges sub-agent lifecycle events from <see cref="IActivityBroadcaster"/> to SignalR
/// session groups so the web UI receives real-time spawned/completed/failed/killed updates.
/// </summary>
public sealed class SubAgentSignalRBridge(
    IActivityBroadcaster activity,
    IHubContext<GatewayHub> hubContext,
    ILogger<SubAgentSignalRBridge> logger) : BackgroundService
{
    private static readonly HashSet<GatewayActivityType> SubAgentEventTypes =
    [
        GatewayActivityType.SubAgentSpawned,
        GatewayActivityType.SubAgentCompleted,
        GatewayActivityType.SubAgentFailed,
        GatewayActivityType.SubAgentKilled
    ];

    private static readonly Dictionary<GatewayActivityType, string> HubMethodMap = new()
    {
        [GatewayActivityType.SubAgentSpawned] = "SubAgentSpawned",
        [GatewayActivityType.SubAgentCompleted] = "SubAgentCompleted",
        [GatewayActivityType.SubAgentFailed] = "SubAgentFailed",
        [GatewayActivityType.SubAgentKilled] = "SubAgentKilled"
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SubAgentSignalRBridge started.");

        try
        {
            await foreach (var evt in activity.SubscribeAsync(stoppingToken).ConfigureAwait(false))
            {
                if (!SubAgentEventTypes.Contains(evt.Type))
                    continue;

                try
                {
                    await ForwardToSignalRAsync(evt, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Failed to forward sub-agent event {EventType} to SignalR.", evt.Type);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }

        logger.LogInformation("SubAgentSignalRBridge stopped.");
    }

    private async Task ForwardToSignalRAsync(GatewayActivity evt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evt.SessionId))
            return;

        if (!HubMethodMap.TryGetValue(evt.Type, out var method))
            return;

        // Extract SubAgentInfo from the activity data
        SubAgentInfo? subAgent = null;
        if (evt.Data?.TryGetValue("subAgent", out var subAgentObj) == true && subAgentObj is SubAgentInfo info)
            subAgent = info;

        if (subAgent is null)
        {
            logger.LogWarning("Sub-agent event {EventType} missing SubAgentInfo in Data.", evt.Type);
            return;
        }

        var parentSessionId = evt.SessionId;
        var group = $"session:{parentSessionId}";

        var payload = new
        {
            sessionId = parentSessionId,
            subAgentId = subAgent.SubAgentId,
            name = subAgent.Name,
            task = subAgent.Task,
            model = subAgent.Model,
            archetype = subAgent.Archetype.Value,
            status = subAgent.Status.ToString(),
            startedAt = subAgent.StartedAt,
            completedAt = subAgent.CompletedAt,
            turnsUsed = subAgent.TurnsUsed,
            resultSummary = subAgent.ResultSummary,
            timedOut = subAgent.Status == SubAgentStatus.TimedOut
        };

        logger.LogDebug("Forwarding {Method} for sub-agent '{SubAgentId}' to group '{Group}'.",
            method, subAgent.SubAgentId, group);

        await hubContext.Clients.Group(group).SendAsync(method, payload, ct).ConfigureAwait(false);
    }
}
