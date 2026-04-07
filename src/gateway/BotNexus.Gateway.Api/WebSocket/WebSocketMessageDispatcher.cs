using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BotNexus.Channels.Core.Diagnostics;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Channels;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Abstractions.Sessions;
using BotNexus.Gateway.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway.Api.WebSocket;
using NetWebSocket = System.Net.WebSockets.WebSocket;
using NetWebSocketCloseStatus = System.Net.WebSockets.WebSocketCloseStatus;
using NetWebSocketMessageType = System.Net.WebSockets.WebSocketMessageType;
using NetWebSocketState = System.Net.WebSockets.WebSocketState;

/// <summary>
/// Dispatches inbound WebSocket messages to the appropriate gateway action and persists outbound stream state.
/// </summary>
public sealed class WebSocketMessageDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAgentSupervisor _supervisor;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IGatewayWebSocketChannelAdapter _channelAdapter;
    private readonly ISessionStore _sessions;
    private readonly IOptions<GatewayWebSocketOptions> _webSocketOptions;
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly ILogger<WebSocketMessageDispatcher> _logger;

    /// <summary>
    /// Initializes a new dispatcher.
    /// </summary>
    public WebSocketMessageDispatcher(
        IAgentSupervisor supervisor,
        IAgentRegistry agentRegistry,
        IGatewayWebSocketChannelAdapter channelAdapter,
        ISessionStore sessions,
        IOptions<GatewayWebSocketOptions> webSocketOptions,
        WebSocketConnectionManager connectionManager,
        ILogger<WebSocketMessageDispatcher> logger)
    {
        _supervisor = supervisor;
        _agentRegistry = agentRegistry;
        _channelAdapter = channelAdapter;
        _sessions = sessions;
        _webSocketOptions = webSocketOptions;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Sends the initial connected event to a newly accepted socket.
    /// </summary>
    internal async Task SendConnectedAsync(
        WebSocketSessionContext sessionContext,
        NetWebSocket socket,
        int replayWindow,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            type = "connected",
            connectionId = sessionContext.ConnectionId,
            sessionId = sessionContext.SessionId,
            agentId = sessionContext.AgentId,
            availableAgents = _agentRegistry.GetAll().Select(agent => new
            {
                agentId = agent.AgentId,
                displayName = agent.DisplayName
            })
        };

        if (sessionContext.HasSession && sessionContext.Session is not null && sessionContext.SessionId is not null)
        {
            await SendSequencedJsonAsync(sessionContext.Session, sessionContext.SessionId, socket, payload, replayWindow, cancellationToken);
            return;
        }

        await SendJsonAsync(socket, payload, cancellationToken);
    }

    /// <summary>
    /// Continuously processes inbound client messages while the socket remains open.
    /// </summary>
    internal async Task ProcessMessagesAsync(
        NetWebSocket socket,
        WebSocketSessionContext sessionContext,
        int replayWindow,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (socket.State == NetWebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == NetWebSocketMessageType.Close)
                break;

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var message = JsonSerializer.Deserialize<WsClientMessage>(json, JsonOptions);
            if (message is null)
                continue;

            if (await _connectionManager.TryHandlePingAsync(
                    message,
                    (payload, ct) => SendWithOptionalSequencingAsync(socket, sessionContext, payload, replayWindow, ct),
                    cancellationToken))
            {
                continue;
            }

            switch (message.Type)
            {
                case "switch_session":
                    await HandleSwitchSessionAsync(socket, sessionContext, message, replayWindow, cancellationToken);
                    break;

                case "message" when message.Content is not null:
                    if (TryResolveSessionResetCommand(message.Content, out _))
                    {
                        await HandleSessionResetAsync(socket, sessionContext, cancellationToken);
                        break;
                    }

                    if (!sessionContext.HasSession || sessionContext.AgentId is null || sessionContext.SessionId is null)
                    {
                        await SendConnectionErrorAsync(
                            socket,
                            sessionContext,
                            replayWindow,
                            "No active session selected. Send switch_session first.",
                            "SESSION_NOT_SELECTED",
                            cancellationToken);
                        break;
                    }

                    await HandleUserMessageAsync(
                        socket,
                        sessionContext.ConnectionId,
                        sessionContext.AgentId,
                        sessionContext.SessionId,
                        message.Content,
                        message.Type,
                        cancellationToken);
                    break;

                case "abort":
                    if (!sessionContext.HasSession || sessionContext.AgentId is null || sessionContext.SessionId is null)
                    {
                        await SendConnectionErrorAsync(
                            socket,
                            sessionContext,
                            replayWindow,
                            "No active session selected. Send switch_session first.",
                            "SESSION_NOT_SELECTED",
                            cancellationToken);
                        break;
                    }

                    await HandleAbortAsync(sessionContext.AgentId, sessionContext.SessionId, cancellationToken);
                    break;

                case "steer" when message.Content is not null:
                    if (!sessionContext.HasSession || sessionContext.AgentId is null || sessionContext.SessionId is null)
                    {
                        await SendConnectionErrorAsync(
                            socket,
                            sessionContext,
                            replayWindow,
                            "No active session selected. Send switch_session first.",
                            "SESSION_NOT_SELECTED",
                            cancellationToken);
                        break;
                    }

                    await HandleSteerAsync(socket, sessionContext.AgentId, sessionContext.SessionId, message.Content, cancellationToken);
                    break;

                case "follow_up" when message.Content is not null:
                    if (!sessionContext.HasSession || sessionContext.AgentId is null || sessionContext.SessionId is null)
                    {
                        await SendConnectionErrorAsync(
                            socket,
                            sessionContext,
                            replayWindow,
                            "No active session selected. Send switch_session first.",
                            "SESSION_NOT_SELECTED",
                            cancellationToken);
                        break;
                    }

                    await HandleFollowUpAsync(socket, sessionContext.AgentId, sessionContext.SessionId, message.Content, cancellationToken);
                    break;

                case "reconnect":
                    var reconnectAgentId = sessionContext.AgentId;
                    var reconnectSessionKey = message.SessionKey ?? sessionContext.SessionId;
                    if (string.IsNullOrWhiteSpace(reconnectSessionKey))
                    {
                        await SendConnectionErrorAsync(
                            socket,
                            sessionContext,
                            replayWindow,
                            "No session available for reconnect.",
                            "SESSION_NOT_FOUND",
                            cancellationToken);
                        break;
                    }

                    await HandleReconnectAsync(
                        socket,
                        reconnectAgentId,
                        reconnectSessionKey,
                        message.LastSeqId ?? 0,
                        replayWindow,
                        cancellationToken);
                    break;

                case "new":
                case "reset":
                    await HandleSessionResetAsync(socket, sessionContext, cancellationToken);
                    break;
            }
        }
    }

    /// <summary>
    /// Switches or initializes the active session for a connected socket.
    /// </summary>
    internal async Task<bool> TrySwitchSessionAsync(
        NetWebSocket socket,
        WebSocketSessionContext sessionContext,
        string? agentId,
        string? sessionId,
        bool includeHistory,
        int? historyLimit,
        int replayWindow,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            await SendConnectionErrorAsync(
                socket,
                sessionContext,
                replayWindow,
                "Missing required 'agentId' for switch_session.",
                "INVALID_SWITCH_SESSION",
                cancellationToken);
            return false;
        }

        if (!_agentRegistry.Contains(agentId))
        {
            await SendConnectionErrorAsync(
                socket,
                sessionContext,
                replayWindow,
                $"Agent '{agentId}' is not registered.",
                "AGENT_NOT_FOUND",
                cancellationToken);
            return false;
        }

        var targetSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? Guid.NewGuid().ToString("N")
            : sessionId;

        var previousAgentId = sessionContext.AgentId;
        var previousSessionId = sessionContext.SessionId;
        var previousSession = sessionContext.Session;

        if (!_connectionManager.TryReserveSession(targetSessionId, sessionContext.ConnectionId, previousSessionId, out _))
        {
            await SendConnectionErrorAsync(
                socket,
                sessionContext,
                replayWindow,
                $"Session '{targetSessionId}' is already connected.",
                "SESSION_ALREADY_CONNECTED",
                cancellationToken);
            return false;
        }

        using var sessionActivity = GatewayDiagnostics.Source.StartActivity("session.get_or_create", ActivityKind.Internal);
        sessionActivity?.SetTag("botnexus.session.id", targetSessionId);
        sessionActivity?.SetTag("botnexus.agent.id", agentId);
        var session = await _sessions.GetOrCreateAsync(targetSessionId, agentId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousSessionId))
            _channelAdapter.UnregisterConnection(previousSessionId, sessionContext.ConnectionId);

        if (!_channelAdapter.RegisterConnection(
                targetSessionId,
                sessionContext.ConnectionId,
                socket,
                (payload, ct) => SequenceAndPersistPayloadAsync(session, payload, replayWindow, ct)))
        {
            _connectionManager.ReleaseSession(targetSessionId, sessionContext.ConnectionId);

            if (!string.IsNullOrWhiteSpace(previousSessionId) && previousSession is not null)
            {
                _connectionManager.TryReserveSession(previousSessionId, sessionContext.ConnectionId);
                _channelAdapter.RegisterConnection(
                    previousSessionId,
                    sessionContext.ConnectionId,
                    socket,
                    (payload, ct) => SequenceAndPersistPayloadAsync(previousSession, payload, replayWindow, ct));
                if (!string.IsNullOrWhiteSpace(previousAgentId))
                    sessionContext.SetCurrentSession(previousAgentId, previousSessionId, previousSession);
            }
            else
            {
                sessionContext.ClearSession();
            }

            await SendConnectionErrorAsync(
                socket,
                sessionContext,
                replayWindow,
                $"Failed to bind connection to session '{targetSessionId}'.",
                "SESSION_BIND_FAILED",
                cancellationToken);
            return false;
        }

        sessionContext.SetCurrentSession(agentId, targetSessionId, session);
        await SendSequencedJsonAsync(
            session,
            targetSessionId,
            socket,
            new
            {
                type = "session_switched",
                sessionId = targetSessionId,
                agentId,
                connectionId = sessionContext.ConnectionId
            },
            replayWindow,
            cancellationToken);

        if (includeHistory)
        {
            var normalizedLimit = Math.Max(historyLimit ?? 100, 1);
            var fullHistory = session.GetHistorySnapshot();
            var history = fullHistory.Skip(Math.Max(fullHistory.Count - normalizedLimit, 0)).ToList();
            await SendSequencedJsonAsync(
                session,
                targetSessionId,
                socket,
                new
                {
                    type = "session_history",
                    sessionId = targetSessionId,
                    agentId,
                    history
                },
                replayWindow,
                cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Allocates sequence IDs, stores replay events, and persists session state for outbound payloads.
    /// </summary>
    public async ValueTask<object> SequenceAndPersistPayloadAsync(
        GatewaySession session,
        object payload,
        int replayWindow,
        CancellationToken cancellationToken)
    {
        var sequenceId = session.ReplayBuffer.AllocateSequenceId();
        var basePayloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var payloadMap = JsonSerializer.Deserialize<Dictionary<string, object?>>(basePayloadJson, JsonOptions) ?? [];
        payloadMap["sequenceId"] = sequenceId;
        object sequencedPayload = payloadMap;
        var sequencedPayloadJson = JsonSerializer.Serialize(sequencedPayload, JsonOptions);
        session.ReplayBuffer.AddStreamEvent(sequenceId, sequencedPayloadJson, replayWindow);
        session.UpdatedAt = DateTimeOffset.UtcNow;

        using var saveActivity = GatewayDiagnostics.Source.StartActivity("session.save", ActivityKind.Internal);
        saveActivity?.SetTag("botnexus.session.id", session.SessionId);
        saveActivity?.SetTag("botnexus.agent.id", session.AgentId);
        await _sessions.SaveAsync(session, cancellationToken);

        return sequencedPayload;
    }

    private async Task HandleSwitchSessionAsync(
        NetWebSocket socket,
        WebSocketSessionContext sessionContext,
        WsClientMessage message,
        int replayWindow,
        CancellationToken cancellationToken)
    {
        await TrySwitchSessionAsync(
            socket,
            sessionContext,
            message.AgentId,
            message.SessionId,
            includeHistory: message.IncludeHistory == true,
            historyLimit: message.HistoryLimit,
            replayWindow,
            cancellationToken);
    }

    private async Task HandleAbortAsync(string agentId, string sessionId, CancellationToken cancellationToken)
    {
        var handle = _supervisor.GetInstance(agentId, sessionId);
        if (handle is null)
            return;

        var agentHandle = await _supervisor.GetOrCreateAsync(agentId, sessionId, cancellationToken);
        await agentHandle.AbortAsync(cancellationToken);
    }

    private async Task HandleUserMessageAsync(
        NetWebSocket socket,
        string connectionId,
        string agentId,
        string sessionId,
        string content,
        string messageType,
        CancellationToken cancellationToken)
    {
        try
        {
            await _channelAdapter.DispatchInboundMessageAsync(
                agentId,
                sessionId,
                connectionId,
                content,
                messageType,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket message for agent '{AgentId}'", agentId);
            await SendSessionErrorAsync(socket, agentId, sessionId, ex.Message, "AGENT_ERROR", cancellationToken);
        }
    }

    private async Task HandleSteerAsync(
        NetWebSocket socket,
        string agentId,
        string sessionId,
        string content,
        CancellationToken cancellationToken)
    {
        using var activity = ChannelDiagnostics.Source.StartActivity("channel.steer", ActivityKind.Internal);
        activity?.SetTag("botnexus.channel.type", "websocket");
        activity?.SetTag("botnexus.message.type", "steer");
        activity?.SetTag("botnexus.session.id", sessionId);
        activity?.SetTag("botnexus.agent.id", agentId);

        var instance = _supervisor.GetInstance(agentId, sessionId);
        if (instance is null)
        {
            await SendSessionErrorAsync(socket, agentId, sessionId, "Agent session not found.", "SESSION_NOT_FOUND", cancellationToken);
            return;
        }

        var handle = await _supervisor.GetOrCreateAsync(agentId, sessionId, cancellationToken);
        await handle.SteerAsync(content, cancellationToken);
    }

    private async Task HandleFollowUpAsync(
        NetWebSocket socket,
        string agentId,
        string sessionId,
        string content,
        CancellationToken cancellationToken)
    {
        var instance = _supervisor.GetInstance(agentId, sessionId);
        if (instance is null)
        {
            await SendSessionErrorAsync(socket, agentId, sessionId, "Agent session not found.", "SESSION_NOT_FOUND", cancellationToken);
            return;
        }

        var handle = await _supervisor.GetOrCreateAsync(agentId, sessionId, cancellationToken);
        await handle.FollowUpAsync(content, cancellationToken);
    }

    private async Task HandleReconnectAsync(
        NetWebSocket socket,
        string? currentAgentId,
        string sessionKey,
        long lastSeqId,
        int replayWindow,
        CancellationToken cancellationToken)
    {
        using var getActivity = GatewayDiagnostics.Source.StartActivity("session.get", ActivityKind.Internal);
        getActivity?.SetTag("botnexus.session.id", sessionKey);

        var session = await _sessions.GetAsync(sessionKey, cancellationToken);
        if (session is null)
        {
            await SendConnectionErrorAsync(
                socket,
                null,
                replayWindow,
                "Session not found for reconnect.",
                "SESSION_NOT_FOUND",
                cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(currentAgentId) &&
            !string.Equals(session.AgentId, currentAgentId, StringComparison.OrdinalIgnoreCase))
        {
            await SendConnectionErrorAsync(
                socket,
                null,
                replayWindow,
                "Session not found for reconnect.",
                "SESSION_NOT_FOUND",
                cancellationToken);
            return;
        }

        var replayEvents = session.ReplayBuffer.GetStreamEventsAfter(lastSeqId, replayWindow);
        foreach (var replayEvent in replayEvents)
        {
            if (socket.State != NetWebSocketState.Open)
                break;

            var payload = Encoding.UTF8.GetBytes(replayEvent.PayloadJson);
            await socket.SendAsync(payload, NetWebSocketMessageType.Text, true, cancellationToken);
        }

        await SendSequencedJsonAsync(
            session,
            sessionKey,
            socket,
            new
            {
                type = "reconnect_ack",
                sessionKey,
                replayed = replayEvents.Count,
                lastSeqId
            },
            replayWindow,
            cancellationToken);
    }

    private async Task HandleSessionResetAsync(
        NetWebSocket socket,
        WebSocketSessionContext sessionContext,
        CancellationToken cancellationToken)
    {
        if (!sessionContext.HasSession || sessionContext.AgentId is null || sessionContext.SessionId is null)
            return;

        var instance = _supervisor.GetInstance(sessionContext.AgentId, sessionContext.SessionId);
        if (instance is not null)
        {
            var stopTask = _supervisor.StopAsync(sessionContext.AgentId, sessionContext.SessionId, cancellationToken);
            if (stopTask is not null)
                await stopTask;
        }

        var session = await _sessions.GetAsync(sessionContext.SessionId, cancellationToken);
        if (session is not null)
        {
            session.Status = SessionStatus.Closed;
            session.UpdatedAt = DateTimeOffset.UtcNow;
            await _sessions.SaveAsync(session, cancellationToken);
        }

        await _sessions.DeleteAsync(sessionContext.SessionId, cancellationToken);
        _channelAdapter.UnregisterConnection(sessionContext.SessionId, sessionContext.ConnectionId);
        _connectionManager.ReleaseSession(sessionContext.SessionId, sessionContext.ConnectionId);
        sessionContext.ClearSession();

        await SendJsonAsync(
            socket,
            new
            {
                type = "session_reset",
                message = "Session reset. System prompt regenerated."
            },
            cancellationToken);

        await socket.CloseAsync(NetWebSocketCloseStatus.NormalClosure, "Session reset", cancellationToken);
    }

    private async Task SendSessionErrorAsync(
        NetWebSocket socket,
        string agentId,
        string sessionId,
        string message,
        string code,
        CancellationToken cancellationToken)
    {
        using var sessionActivity = GatewayDiagnostics.Source.StartActivity("session.get_or_create", ActivityKind.Internal);
        sessionActivity?.SetTag("botnexus.session.id", sessionId);
        sessionActivity?.SetTag("botnexus.agent.id", agentId);

        var session = await _sessions.GetOrCreateAsync(sessionId, agentId, cancellationToken);
        await SendSequencedJsonAsync(
            session,
            sessionId,
            socket,
            new { type = "error", message, code },
            Math.Max(_webSocketOptions.Value.ReplayWindowSize, 1),
            cancellationToken);
    }

    private async Task SendConnectionErrorAsync(
        NetWebSocket socket,
        WebSocketSessionContext? sessionContext,
        int replayWindow,
        string message,
        string code,
        CancellationToken cancellationToken)
    {
        if (sessionContext is not null &&
            sessionContext.HasSession &&
            sessionContext.Session is not null &&
            sessionContext.SessionId is not null)
        {
            await SendSequencedJsonAsync(
                sessionContext.Session,
                sessionContext.SessionId,
                socket,
                new { type = "error", message, code },
                replayWindow,
                cancellationToken);
            return;
        }

        await SendJsonAsync(socket, new { type = "error", message, code }, cancellationToken);
    }

    private async Task SendWithOptionalSequencingAsync(
        NetWebSocket socket,
        WebSocketSessionContext sessionContext,
        object payload,
        int replayWindow,
        CancellationToken cancellationToken)
    {
        if (sessionContext.HasSession && sessionContext.Session is not null && sessionContext.SessionId is not null)
        {
            await SendSequencedJsonAsync(
                sessionContext.Session,
                sessionContext.SessionId,
                socket,
                payload,
                replayWindow,
                cancellationToken);
            return;
        }

        await SendJsonAsync(socket, payload, cancellationToken);
    }

    private async Task SendSequencedJsonAsync(
        GatewaySession session,
        string sessionId,
        NetWebSocket socket,
        object message,
        int replayWindow,
        CancellationToken cancellationToken)
    {
        var sequenced = await SequenceAndPersistPayloadAsync(session, message, replayWindow, cancellationToken);
        var json = JsonSerializer.SerializeToUtf8Bytes(sequenced, JsonOptions);
        await socket.SendAsync(json, NetWebSocketMessageType.Text, true, cancellationToken);
    }

    private static Task SendJsonAsync(NetWebSocket socket, object message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        return socket.SendAsync(json, NetWebSocketMessageType.Text, true, cancellationToken);
    }

    private static bool TryResolveSessionResetCommand(string? content, out string commandType)
    {
        commandType = string.Empty;
        var normalized = content?.Trim();
        if (string.IsNullOrEmpty(normalized))
            return false;

        if (string.Equals(normalized, "/new", StringComparison.OrdinalIgnoreCase))
        {
            commandType = "new";
            return true;
        }

        if (string.Equals(normalized, "/reset", StringComparison.OrdinalIgnoreCase))
        {
            commandType = "reset";
            return true;
        }

        return false;
    }
}
