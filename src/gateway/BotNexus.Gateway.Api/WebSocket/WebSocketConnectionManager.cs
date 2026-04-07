using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway.Api.WebSocket;

/// <summary>
/// Manages WebSocket connection admission, session locks, reconnect throttling, and keepalive pong responses.
/// </summary>
public sealed class WebSocketConnectionManager
{
    /// <summary>
    /// Custom close code returned when a session already has an active WebSocket connection.
    /// </summary>
    public const int SessionAlreadyConnectedCloseCode = 4409;

    private readonly IOptions<GatewayWebSocketOptions> _webSocketOptions;
    private readonly ILogger<WebSocketConnectionManager> _logger;
    private readonly ConcurrentDictionary<string, ConnectionAttemptWindow> _connectionAttempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _activeSessionConnections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _connectionSessions = new(StringComparer.Ordinal);
    private readonly Lock _sessionSync = new();
    private long _connectionAttemptUpdates;

    /// <summary>
    /// Initializes a new connection manager.
    /// </summary>
    public WebSocketConnectionManager(
        IOptions<GatewayWebSocketOptions> webSocketOptions,
        ILogger<WebSocketConnectionManager> logger)
    {
        _webSocketOptions = webSocketOptions;
        _logger = logger;
    }

    /// <summary>
    /// Registers a reconnect attempt and enforces throttling windows.
    /// Loopback clients (localhost) are always allowed — matches OpenClaw's exemptLoopback pattern.
    /// </summary>
    public bool TryRegisterConnectionAttempt(HttpContext context, out TimeSpan retryAfter)
    {
        // Exempt loopback/localhost clients from reconnect throttling
        if (IsLoopback(context))
        {
            retryAfter = TimeSpan.Zero;
            return true;
        }

        var options = _webSocketOptions.Value;
        var maxAttempts = Math.Max(options.MaxReconnectAttempts, 1);
        var attemptWindow = TimeSpan.FromSeconds(Math.Max(options.AttemptWindowSeconds, 1));
        var backoffBase = TimeSpan.FromSeconds(Math.Max(options.BackoffBaseSeconds, 1));
        var backoffMax = TimeSpan.FromSeconds(Math.Max(options.BackoffMaxSeconds, options.BackoffBaseSeconds));
        var now = DateTimeOffset.UtcNow;
        var clientKey = GetClientAttemptKey(context);

        while (true)
        {
            if (!_connectionAttempts.TryGetValue(clientKey, out var current))
            {
                if (_connectionAttempts.TryAdd(clientKey, new ConnectionAttemptWindow(now, 1)))
                {
                    retryAfter = TimeSpan.Zero;
                    CleanupStaleAttemptWindows(attemptWindow, now);
                    return true;
                }

                continue;
            }

            if (now - current.WindowStartedUtc >= attemptWindow)
            {
                if (_connectionAttempts.TryUpdate(clientKey, new ConnectionAttemptWindow(now, 1), current))
                {
                    retryAfter = TimeSpan.Zero;
                    CleanupStaleAttemptWindows(attemptWindow, now);
                    return true;
                }

                continue;
            }

            if (current.AttemptCount >= maxAttempts)
            {
                var penaltyAttempt = current.AttemptCount - maxAttempts + 1;
                var retrySeconds = Math.Min(
                    backoffBase.TotalSeconds * Math.Pow(2, penaltyAttempt - 1),
                    backoffMax.TotalSeconds);
                retryAfter = TimeSpan.FromSeconds(Math.Max(1, Math.Ceiling(retrySeconds)));
                return false;
            }

            var updated = current with { AttemptCount = current.AttemptCount + 1 };
            if (_connectionAttempts.TryUpdate(clientKey, updated, current))
            {
                retryAfter = TimeSpan.Zero;
                CleanupStaleAttemptWindows(attemptWindow, now);
                return true;
            }
        }
    }

    /// <summary>
    /// Reserves a session slot for an active WebSocket connection.
    /// </summary>
    public bool TryReserveSession(string sessionId, string connectionId)
        => TryReserveSession(sessionId, connectionId, previousSessionId: null, out _);

    /// <summary>
    /// Reserves a session slot for an active WebSocket connection, releasing a previous session reservation when switching.
    /// </summary>
    public bool TryReserveSession(string sessionId, string connectionId, string? previousSessionId, out string? conflictingConnectionId)
    {
        conflictingConnectionId = null;
        lock (_sessionSync)
        {
            if (_activeSessionConnections.TryGetValue(sessionId, out var existingConnectionId) &&
                !string.Equals(existingConnectionId, connectionId, StringComparison.Ordinal))
            {
                conflictingConnectionId = existingConnectionId;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(previousSessionId))
            {
                ReleaseSessionInternal(previousSessionId, connectionId);
            }
            else if (_connectionSessions.TryGetValue(connectionId, out var reservedSessionId) &&
                     !string.Equals(reservedSessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            {
                ReleaseSessionInternal(reservedSessionId, connectionId);
            }

            _activeSessionConnections[sessionId] = connectionId;
            _connectionSessions[connectionId] = sessionId;
            return true;
        }
    }

    /// <summary>
    /// Closes a duplicate session connection with the gateway's session-conflict code.
    /// </summary>
    public async Task CloseDuplicateSessionAsync(HttpContext context, CancellationToken cancellationToken)
    {
        using var duplicateSocket = await context.WebSockets.AcceptWebSocketAsync();
        await duplicateSocket.CloseAsync(
            (System.Net.WebSockets.WebSocketCloseStatus)SessionAlreadyConnectedCloseCode,
            "Session already has an active connection",
            cancellationToken);
    }

    /// <summary>
    /// Releases a reserved session slot and resets reconnect throttling for the client.
    /// </summary>
    public void ReleaseSession(string sessionId, string connectionId)
    {
        lock (_sessionSync)
        {
            if (!string.IsNullOrWhiteSpace(sessionId))
                ReleaseSessionInternal(sessionId, connectionId);

            if (_connectionSessions.TryGetValue(connectionId, out var reservedSessionId))
                ReleaseSessionInternal(reservedSessionId, connectionId);
        }

        // Reset reconnect throttling on clean disconnect.
        _connectionAttempts.Clear();
    }

    /// <summary>
    /// Handles keepalive ping messages by sending a pong payload through the sequenced sender.
    /// </summary>
    internal Task<bool> TryHandlePingAsync(
        WsClientMessage message,
        Func<object, CancellationToken, Task> sendAsync,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(message.Type, "ping", StringComparison.Ordinal))
            return Task.FromResult(false);

        _logger.LogTrace("WebSocket ping received; responding with pong.");
        return SendPongAsync(sendAsync, cancellationToken);
    }

    private static async Task<bool> SendPongAsync(
        Func<object, CancellationToken, Task> sendAsync,
        CancellationToken cancellationToken)
    {
        await sendAsync(new { type = "pong" }, cancellationToken);
        return true;
    }

    private static string GetClientAttemptKey(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var clientAddress = context.Connection.RemoteIpAddress?.ToString();
        var clientId = string.IsNullOrWhiteSpace(forwardedFor)
            ? clientAddress
            : forwardedFor.Split(',')[0].Trim();

        return string.IsNullOrWhiteSpace(clientId) ? "unknown" : clientId;
    }

    private void CleanupStaleAttemptWindows(TimeSpan attemptWindow, DateTimeOffset now)
    {
        if (Interlocked.Increment(ref _connectionAttemptUpdates) % 128 != 0)
            return;

        foreach (var (key, value) in _connectionAttempts)
        {
            if (now - value.WindowStartedUtc >= attemptWindow + attemptWindow)
                _connectionAttempts.TryRemove(key, out _);
        }
    }

    private readonly record struct ConnectionAttemptWindow(DateTimeOffset WindowStartedUtc, int AttemptCount);

    private void ReleaseSessionInternal(string sessionId, string connectionId)
    {
        if (_activeSessionConnections.TryGetValue(sessionId, out var existingConnectionId) &&
            string.Equals(existingConnectionId, connectionId, StringComparison.Ordinal))
        {
            _activeSessionConnections.Remove(sessionId);
        }

        if (_connectionSessions.TryGetValue(connectionId, out var existingSessionId) &&
            string.Equals(existingSessionId, sessionId, StringComparison.OrdinalIgnoreCase))
        {
            _connectionSessions.Remove(connectionId);
        }
    }

    private static bool IsLoopback(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        return remote is not null && (System.Net.IPAddress.IsLoopback(remote) || remote.Equals(System.Net.IPAddress.IPv6Loopback));
    }
}
