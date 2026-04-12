using BotNexus.Gateway.Abstractions.Activity;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Channels;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Abstractions.Sessions;
using AgentId = BotNexus.Domain.Primitives.AgentId;
using ChannelKey = BotNexus.Domain.Primitives.ChannelKey;
using ParticipantType = BotNexus.Domain.Primitives.ParticipantType;
using SessionId = BotNexus.Domain.Primitives.SessionId;
using SessionParticipant = BotNexus.Domain.Primitives.SessionParticipant;
using SessionType = BotNexus.Domain.Primitives.SessionType;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway.Api.Hubs;

#pragma warning disable CS1591 // Hub methods are self-documenting SignalR contracts

/// <summary>
/// SignalR hub for real-time agent communication. Replaces the raw WebSocket infrastructure.
/// Clients join session groups and receive streaming output for all active sessions simultaneously.
/// </summary>
public sealed class GatewayHub : Hub
{
    private readonly IAgentSupervisor _supervisor;
    private readonly IAgentRegistry _registry;
    private readonly ISessionStore _sessions;
    private readonly IChannelDispatcher _dispatcher;
    private readonly IActivityBroadcaster _activity;
    private readonly ISessionCompactor _compactor;
    private readonly ISessionWarmupService _warmup;
    private readonly IOptions<CompactionOptions> _compactionOptions;
    private readonly ILogger<GatewayHub> _logger;

    public GatewayHub(
        IAgentSupervisor supervisor,
        IAgentRegistry registry,
        ISessionStore sessions,
        IChannelDispatcher dispatcher,
        IActivityBroadcaster activity,
        ISessionCompactor compactor,
        ISessionWarmupService warmup,
        IOptions<CompactionOptions> compactionOptions,
        ILogger<GatewayHub> logger)
    {
        _supervisor = supervisor;
        _registry = registry;
        _sessions = sessions;
        _dispatcher = dispatcher;
        _activity = activity;
        _compactor = compactor;
        _warmup = warmup;
        _compactionOptions = compactionOptions;
        _logger = logger;
    }

    public async Task<object> SubscribeAll()
    {
        var sessions = await _warmup.GetAvailableSessionsAsync(Context.ConnectionAborted);

        foreach (var session in sessions)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GetSessionGroup(session.SessionId),
                Context.ConnectionAborted);
        }

        _logger.LogInformation(
            "Hub SubscribeAll: connection={ConnectionId} sessions={Count}",
            Context.ConnectionId,
            sessions.Count);

        return new { sessions };
    }

    public async Task<object> Subscribe(string sessionId)
    {
        var typedSessionId = ParseSessionId(sessionId);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetSessionGroup(typedSessionId),
            Context.ConnectionAborted);

        return new
        {
            sessionId = typedSessionId.Value,
            status = "subscribed"
        };
    }

    public async Task<object> JoinSession(string agentId, string? sessionId)
    {
        var typedAgentId = ParseAgentId(agentId);
        var typedSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? SessionId.Create()
            : ParseSessionId(sessionId);

        _logger.LogInformation("Hub JoinSession: agent={AgentId} session={SessionId} connection={ConnectionId} group={Group}",
            typedAgentId, typedSessionId, Context.ConnectionId, GetSessionGroup(typedSessionId));
        await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroup(typedSessionId), Context.ConnectionAborted);

        var session = await _sessions.GetOrCreateAsync(typedSessionId, typedAgentId, Context.ConnectionAborted);

        var needsSave = false;
        if (session.Status == SessionStatus.Expired)
        {
            _logger.LogInformation("Reactivating expired session {SessionId} on join", typedSessionId);
            session.Status = SessionStatus.Active;
            session.ExpiresAt = null;
            needsSave = true;
        }

        if (session.ChannelType is null)
        {
            session.ChannelType = ChannelKey.From("signalr");
            needsSave = true;
        }

        session.SessionType = SessionType.UserAgent;
        if (session.Participants.Count == 0)
        {
            session.Participants.Add(new SessionParticipant
            {
                Type = ParticipantType.User,
                Id = Context.ConnectionId
            });
            needsSave = true;
        }

        if (needsSave)
        {
            await _sessions.SaveAsync(session, Context.ConnectionAborted);
        }

        return new
        {
            sessionId = session.SessionId.Value,
            agentId = session.AgentId.Value,
            connectionId = Context.ConnectionId,
            messageCount = session.History.Count,
            isResumed = session.History.Count > 0,
            status = session.Status.ToString(),
            channelType = session.ChannelType,
            createdAt = session.CreatedAt,
            updatedAt = session.UpdatedAt
        };
    }

    public Task LeaveSession(string sessionId)
        => Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetSessionGroup(ParseSessionId(sessionId)),
            Context.ConnectionAborted);

    public Task SendMessage(string agentId, string sessionId, string content)
    {
        var typedAgentId = ParseAgentId(agentId);
        var typedSessionId = ParseSessionId(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        _logger.LogInformation("Hub SendMessage: agent={AgentId} session={SessionId} connection={ConnectionId} content={Content}",
            typedAgentId, typedSessionId, Context.ConnectionId, content.Length > 50 ? content[..50] + "..." : content);
        return _dispatcher.DispatchAsync(
            new InboundMessage
            {
                ChannelType = ChannelKey.From("signalr"),
                SenderId = Context.ConnectionId,
                ConversationId = typedSessionId.Value,
                SessionId = typedSessionId.Value,
                TargetAgentId = typedAgentId.Value,
                Content = content,
                Metadata = new Dictionary<string, object?> { ["messageType"] = "message" }
            },
            CancellationToken.None);
    }

    public Task Steer(string agentId, string sessionId, string content)
    {
        var typedAgentId = ParseAgentId(agentId);
        var typedSessionId = ParseSessionId(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return _dispatcher.DispatchAsync(
            new InboundMessage
            {
                ChannelType = ChannelKey.From("signalr"),
                SenderId = Context.ConnectionId,
                ConversationId = typedSessionId.Value,
                SessionId = typedSessionId.Value,
                TargetAgentId = typedAgentId.Value,
                Content = content,
                Metadata = new Dictionary<string, object?>
                {
                    ["messageType"] = "steer",
                    ["control"] = "steer"
                }
            },
            CancellationToken.None);
    }

    public Task FollowUp(string agentId, string sessionId, string content)
        => SendMessage(agentId, sessionId, content);

    public async Task Abort(string agentId, string sessionId)
    {
        var typedAgentId = ParseAgentId(agentId);
        var typedSessionId = ParseSessionId(sessionId);
        var instance = _supervisor.GetInstance(typedAgentId, typedSessionId);
        if (instance is null)
            return;

        var handle = await _supervisor.GetOrCreateAsync(typedAgentId, typedSessionId, CancellationToken.None);
        await handle.AbortAsync(CancellationToken.None);
    }

    public async Task ResetSession(string agentId, string sessionId)
    {
        var typedAgentId = ParseAgentId(agentId);
        var typedSessionId = ParseSessionId(sessionId);
        await _supervisor.StopAsync(typedAgentId, typedSessionId, CancellationToken.None);
        await _sessions.ArchiveAsync(typedSessionId, CancellationToken.None);
        await Clients.Caller.SendAsync("SessionReset", new { agentId = typedAgentId.Value, sessionId = typedSessionId.Value });
    }

    public async Task<object> CompactSession(string agentId, string sessionId)
    {
        _ = ParseAgentId(agentId);
        var typedSessionId = ParseSessionId(sessionId);
        var session = await _sessions.GetAsync(typedSessionId, CancellationToken.None);
        if (session is null)
            throw new HubException($"Session '{typedSessionId.Value}' not found.");

        var requestServices = Context.GetHttpContext()?.RequestServices;
        var compactor = requestServices?.GetService<ISessionCompactor>() ?? _compactor;
        var options = requestServices?.GetService<IOptions<CompactionOptions>>()?.Value ?? _compactionOptions.Value;

        var result = await compactor.CompactAsync(session.Session, options, CancellationToken.None);
        await _sessions.SaveAsync(session, CancellationToken.None);

        return new
        {
            summarized = result.EntriesSummarized,
            preserved = result.EntriesPreserved,
            tokensBefore = result.TokensBefore,
            tokensAfter = result.TokensAfter
        };
    }

    public Task<IReadOnlyList<AgentDescriptor>> GetAgents()
        => Task.FromResult(_registry.GetAll());

    public AgentInstance? GetAgentStatus(string agentId, string sessionId)
        => _supervisor.GetInstance(ParseAgentId(agentId), ParseSessionId(sessionId));

    public override async Task OnConnectedAsync()
    {
        var clientVersion = Context.GetHttpContext()?.Request.Query["clientVersion"].FirstOrDefault() ?? "unknown";
        _logger.LogInformation("Hub OnConnected: connection={ConnectionId} clientVersion={ClientVersion}",
            Context.ConnectionId, clientVersion);

        await Clients.Caller.SendAsync("Connected", new
        {
            connectionId = Context.ConnectionId,
            agents = _registry.GetAll().Select(a => new { a.AgentId, a.DisplayName }),
            serverVersion = typeof(GatewayHub).Assembly.GetName().Version?.ToString() ?? "dev",
            capabilities = new { multiSession = true }
        });

        await _activity.PublishAsync(
            new GatewayActivity
            {
                Type = GatewayActivityType.System,
                ChannelType = ChannelKey.From("signalr"),
                Message = "Web Chat client connected.",
                Data = new Dictionary<string, object?> { ["connectionId"] = Context.ConnectionId }
            },
            Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    private static AgentId ParseAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new HubException("Agent ID is required.");

        return AgentId.From(agentId);
    }

    private static SessionId ParseSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new HubException("Session ID is required.");

        return SessionId.From(sessionId);
    }

    private static string GetSessionGroup(SessionId sessionId) => $"session:{sessionId.Value}";
    private static string GetSessionGroup(string sessionId) => $"session:{sessionId}";
}
