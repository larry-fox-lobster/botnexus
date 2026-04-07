using BotNexus.Gateway.Abstractions.Models;

namespace BotNexus.Gateway.Api.WebSocket;

/// <summary>
/// Mutable per-connection session state for WebSocket message dispatch.
/// </summary>
internal sealed class WebSocketSessionContext(string connectionId)
{
    public string ConnectionId { get; } = connectionId;

    public string? AgentId { get; private set; }

    public string? SessionId { get; private set; }

    public GatewaySession? Session { get; private set; }

    public bool HasSession => Session is not null && !string.IsNullOrWhiteSpace(SessionId) && !string.IsNullOrWhiteSpace(AgentId);

    public void SetCurrentSession(string agentId, string sessionId, GatewaySession session)
    {
        AgentId = agentId;
        SessionId = sessionId;
        Session = session;
    }

    public void ClearSession()
    {
        AgentId = null;
        SessionId = null;
        Session = null;
    }
}
