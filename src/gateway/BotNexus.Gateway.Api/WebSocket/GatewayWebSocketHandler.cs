using BotNexus.Gateway.Abstractions.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway.Api.WebSocket;
using NetWebSocket = System.Net.WebSockets.WebSocket;
using NetWebSocketError = System.Net.WebSockets.WebSocketError;
using NetWebSocketException = System.Net.WebSockets.WebSocketException;

/// <summary>
/// Handles WebSocket connections for real-time agent interaction.
/// </summary>
/// <remarks>
/// <para>WebSocket Protocol:</para>
/// <para><b>Connection:</b> <c>ws://host/ws</c> (optional: <c>?agent={agentId}&amp;session={sessionId}</c>)</para>
/// <para><b>Client → Server messages:</b></para>
/// <list type="bullet">
///   <item><c>{ "type": "switch_session", "agentId": "...", "sessionId": "optional" }</c> — Switch active session.</item>
///   <item><c>{ "type": "message", "content": "..." }</c> — Send a message to the agent.</item>
///   <item><c>{ "type": "reconnect", "sessionKey": "...", "lastSeqId": 42 }</c> — Replay missed outbound events.</item>
///   <item><c>{ "type": "abort" }</c> — Abort the current agent execution.</item>
///   <item><c>{ "type": "steer", "content": "..." }</c> — Inject steering message into active run.</item>
///   <item><c>{ "type": "follow_up", "content": "..." }</c> — Queue follow-up for next run.</item>
///   <item><c>{ "type": "new" }</c> — Reset current session and start a new chat.</item>
///   <item><c>{ "type": "reset" }</c> — Reset current session and regenerate system prompt.</item>
///   <item><c>{ "type": "ping" }</c> — Keepalive ping.</item>
/// </list>
/// <para><b>Server → Client messages:</b></para>
/// <list type="bullet">
///   <item><c>{ "type": "connected", "connectionId": "...", "sessionId": "...", "agentId": "...", "availableAgents": [ ... ] }</c></item>
///   <item><c>{ "type": "session_switched", "connectionId": "...", "sessionId": "...", "agentId": "...", "sequenceId": 2 }</c></item>
///   <item><c>{ "type": "message_start", "messageId": "..." }</c></item>
///   <item><c>{ "type": "thinking_delta", "delta": "...", "messageId": "..." }</c></item>
///   <item><c>{ "type": "content_delta", "delta": "...", "messageId": "..." }</c></item>
///   <item><c>{ "type": "tool_start", "toolCallId": "...", "toolName": "...", "messageId": "..." }</c></item>
///   <item><c>{ "type": "tool_end", "toolCallId": "...", "toolName": "...", "toolResult": "...", "toolIsError": false, "messageId": "..." }</c></item>
///   <item><c>{ "type": "message_end", "messageId": "...", "usage": { ... } }</c></item>
///   <item><c>{ "type": "error", "message": "...", "code": "..." }</c></item>
///   <item><c>{ "type": "session_reset", "sessionId": "...", "message": "..." }</c></item>
///   <item><c>{ "type": "pong" }</c></item>
/// </list>
/// </remarks>
public sealed class GatewayWebSocketHandler
{
    private readonly IGatewayWebSocketChannelAdapter _channelAdapter;
    private readonly IOptions<GatewayWebSocketOptions> _webSocketOptions;
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly WebSocketMessageDispatcher _dispatcher;
    private readonly ILogger<GatewayWebSocketHandler> _logger;

    /// <summary>
    /// Initializes a new handler that orchestrates connection lifecycle and message dispatch.
    /// </summary>
    public GatewayWebSocketHandler(
        IGatewayWebSocketChannelAdapter channelAdapter,
        IOptions<GatewayWebSocketOptions> webSocketOptions,
        WebSocketConnectionManager connectionManager,
        WebSocketMessageDispatcher dispatcher,
        ILogger<GatewayWebSocketHandler> logger)
    {
        _channelAdapter = channelAdapter;
        _webSocketOptions = webSocketOptions;
        _connectionManager = connectionManager;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Handles an incoming WebSocket connection.
    /// </summary>
    public async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var initialAgentId = context.Request.Query["agent"].FirstOrDefault();
        var initialSessionId = context.Request.Query["session"].FirstOrDefault();

        if (!_connectionManager.TryRegisterConnectionAttempt(context, out var retryAfter))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            var retrySeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            if (retrySeconds > 0)
                context.Response.Headers["Retry-After"] = retrySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

            await context.Response.WriteAsync(
                $"Reconnect limit exceeded. Retry in {Math.Max(retrySeconds, 1)} second(s).",
                cancellationToken);
            return;
        }

        var connectionId = Guid.NewGuid().ToString("N");

        NetWebSocket? socket = null;
        var sessionContext = new WebSocketSessionContext(connectionId);
        var replayWindow = Math.Max(_webSocketOptions.Value.ReplayWindowSize, 1);
        try
        {
            socket = await context.WebSockets.AcceptWebSocketAsync();
            await _dispatcher.SendConnectedAsync(sessionContext, socket, replayWindow, cancellationToken);

            if (!string.IsNullOrWhiteSpace(initialAgentId))
            {
                var resolvedSessionId = string.IsNullOrWhiteSpace(initialSessionId)
                    ? Guid.NewGuid().ToString("N")
                    : initialSessionId;

                await _dispatcher.TrySwitchSessionAsync(
                    socket,
                    sessionContext,
                    initialAgentId,
                    resolvedSessionId,
                    includeHistory: false,
                    historyLimit: null,
                    replayWindow,
                    cancellationToken);
            }

            _logger.LogInformation(
                "WebSocket connected: {ConnectionId} agent={AgentId} session={SessionId}",
                connectionId,
                sessionContext.AgentId ?? "(none)",
                sessionContext.SessionId ?? "(none)");

            await _dispatcher.ProcessMessagesAsync(socket, sessionContext, replayWindow, cancellationToken);
        }
        catch (NetWebSocketException ex) when (ex.WebSocketErrorCode == NetWebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogDebug("WebSocket closed prematurely: {ConnectionId}", connectionId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("WebSocket cancelled: {ConnectionId}", connectionId);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(sessionContext.SessionId))
                _channelAdapter.UnregisterConnection(sessionContext.SessionId, connectionId);
            socket?.Dispose();
            _connectionManager.ReleaseSession(sessionContext.SessionId ?? string.Empty, connectionId);
            _logger.LogInformation("WebSocket disconnected: {ConnectionId}", connectionId);
        }
    }
}
