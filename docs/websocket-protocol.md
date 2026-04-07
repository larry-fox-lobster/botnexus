# SignalR Hub Contract

> This document replaces the retired raw WebSocket protocol.  
> Real-time gateway traffic now uses SignalR at `/hub/gateway`.

## Endpoint

- **Hub URL:** `http://localhost:5005/hub/gateway`
- **Transport:** SignalR negotiation (WebSockets/Server-Sent Events/Long Polling as available)

## Hub Methods (Client → Server)

- `JoinSession(agentId, sessionId?)`
- `LeaveSession(sessionId)`
- `SendMessage(agentId, sessionId, content)`
- `Steer(agentId, sessionId, content)`
- `FollowUp(agentId, sessionId, content)`
- `Abort(agentId, sessionId)`
- `ResetSession(agentId, sessionId)`
- `GetAgents()`
- `GetAgentStatus(agentId, sessionId)`

## Server Events (Server → Client)

- `Connected`
- `SessionJoined`
- `SessionReset`
- `MessageStart`
- `ThinkingDelta`
- `ContentDelta`
- `ToolStart`
- `ToolEnd`
- `MessageEnd`
- `Error`

## Notes

- All gateway-originated messages use `channelType = "signalr"`.
- Clients should call `JoinSession` before sending messages for a session.
- Session fan-out uses SignalR groups: `session:{sessionId}`.
