# BotNexus.Channels.WebSocket

> Real-time WebSocket channel adapter for streaming agent interaction.

## Overview

This package provides the WebSocket channel adapter that connects the BotNexus Gateway to browser and API clients over persistent WebSocket connections. It derives from `ChannelAdapterBase` and implements `IGatewayWebSocketChannelAdapter`, supporting full-duplex streaming with sequence-tracked outbound events, reconnection with replay, and rich interaction controls (steering, follow-up, abort).

**Status: Implemented** — Full streaming protocol with sequenced events, reconnection replay, ping/pong keepalive, and all five capability flags enabled.

## Key Types

| Type | Kind | Description |
|------|------|-------------|
| `WebSocketChannelAdapter` | Class | WebSocket adapter. Manages live connections, dispatches inbound messages, and streams outbound events (content, thinking, tool calls) to connected clients. |
| `WebSocketServiceCollectionExtensions` | Static class | DI registration extension method `AddBotNexusWebSocketChannel()`. |

## Current Capabilities

| Feature | Status | Notes |
|---------|--------|-------|
| Outbound streaming | ✅ Working | Streams `content_delta`, `thinking_delta`, `tool_start`, `tool_end`, `message_start`, `message_end`, and `error` events |
| Inbound messages | ✅ Working | Receives `message`, `abort`, `steer`, `follow_up`, `reconnect`, and `ping` from clients |
| Sequence tracking | ✅ Working | Every outbound message receives a monotonically increasing `sequenceId` |
| Reconnection replay | ✅ Working | Clients send `reconnect` with `lastSeqId` to replay missed events from the replay buffer |
| Ping/pong keepalive | ✅ Working | Client sends `ping`, server responds with `pong` |
| Steering | ✅ Working | Inject guidance into an active agent run via `steer` messages |
| Follow-up | ✅ Working | Queue follow-up content for the next agent run via `follow_up` messages |
| Abort | ✅ Working | Cancel an active agent execution via `abort` messages |
| Payload mutation | ✅ Working | Supports a `payloadMutator` callback for sequencing and persistence before send |
| Session locking | ✅ Working | One active WebSocket per session; duplicates receive close code `4409` |

### What It Does Now

- Registers as channel type `"websocket"` with display name `"Gateway WebSocket"`
- Reports all capability flags: `SupportsStreaming`, `SupportsSteering`, `SupportsFollowUp`, `SupportsThinkingDisplay`, `SupportsToolDisplay`
- Maintains a `ConcurrentDictionary` of active session → connection registrations
- On `RegisterConnection`: associates a session with a live `WebSocket` and optional payload mutator
- On `UnregisterConnection`: removes the registration (only if the connection ID matches)
- On `SendAsync`: sends a `content_delta` event with the full message content
- On `SendStreamDeltaAsync`: sends a `content_delta` event with the incremental delta
- On `SendStreamEventAsync`: maps `AgentStreamEvent` to the appropriate outbound event type
- On `OnStartAsync`/`OnStopAsync`: no-op start; clears all connections on stop

## Message Protocol

### Connection

```
ws://host/ws?agent={agentId}&session={sessionId}
```

The `agent` query parameter is required. The `session` parameter is optional — when omitted, the server generates a new session ID.

### Client → Server (Inbound)

| Type | Fields | Description |
|------|--------|-------------|
| `message` | `content` | Send a user message to the agent |
| `reconnect` | `sessionKey`, `lastSeqId` | Replay missed events after a disconnection |
| `abort` | — | Cancel the current agent execution |
| `steer` | `content` | Inject steering guidance into an active run |
| `follow_up` | `content` | Queue a follow-up message for the next run |
| `ping` | — | Keepalive ping; server responds with `pong` |

### Server → Client (Outbound)

All outbound messages include a `sequenceId` field for ordering and reconnection support.

| Type | Fields | Description |
|------|--------|-------------|
| `connected` | `connectionId`, `sessionId` | Sent immediately after WebSocket acceptance |
| `message_start` | `messageId` | Marks the beginning of an agent response |
| `thinking_delta` | `delta`, `messageId` | Incremental thinking/reasoning content |
| `content_delta` | `delta`, `messageId` | Incremental response content |
| `tool_start` | `toolCallId`, `toolName`, `messageId` | Agent has started a tool invocation |
| `tool_end` | `toolCallId`, `toolName`, `toolResult`, `toolIsError`, `messageId` | Tool invocation completed |
| `message_end` | `messageId`, `usage` | Marks the end of an agent response, includes token usage |
| `error` | `message` | Error notification |
| `pong` | — | Response to client `ping` |
| `reconnect_ack` | `sessionKey`, `replayed`, `lastSeqId` | Confirms reconnection; reports how many events were replayed |

## Reconnection Flow

The WebSocket protocol supports transparent reconnection through sequence IDs and a server-side replay buffer.

1. Every outbound message is assigned a monotonically increasing `sequenceId`
2. The server retains recent outbound events in a replay buffer (default: 1000 events)
3. When a client reconnects, it sends a `reconnect` message with its `lastSeqId`
4. The server replays all buffered events with `sequenceId > lastSeqId`
5. A `reconnect_ack` message confirms completion with the count of replayed events

```
Client                          Server
  │                               │
  │── reconnect ─────────────────►│  { type: "reconnect", sessionKey: "abc", lastSeqId: 42 }
  │                               │
  │◄── replay event (seqId: 43) ──│
  │◄── replay event (seqId: 44) ──│
  │◄── replay event (seqId: 45) ──│
  │                               │
  │◄── reconnect_ack ────────────│  { type: "reconnect_ack", replayed: 3, lastSeqId: 42 }
  │                               │
```

## Configuration

The adapter itself has no direct configuration options. Runtime behavior is controlled through `GatewayWebSocketOptions` in the Gateway API:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `MaxReconnectAttempts` | `int` | `20` | Maximum reconnection attempts per client/agent within the tracking window |
| `AttemptWindowSeconds` | `int` | `300` | Sliding window duration (seconds) for counting reconnect attempts |
| `BackoffBaseSeconds` | `int` | `1` | Base retry delay (seconds) returned after hitting reconnect limits |
| `BackoffMaxSeconds` | `int` | `60` | Maximum retry delay (seconds) with exponential backoff |
| `ReplayWindowSize` | `int` | `1000` | Maximum number of sequenced outbound messages retained for reconnect replay |

## Usage

### Registration

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBotNexusWebSocketChannel();
```

The extension method registers `WebSocketChannelAdapter` as both `IGatewayWebSocketChannelAdapter` and `IChannelAdapter` in the DI container using `TryAddSingleton` to prevent duplicate registrations.

### Connecting via WebSocket

```javascript
const ws = new WebSocket("ws://localhost:5005/ws?agent=my-agent&session=my-session");

ws.onopen = () => {
    console.log("Connected");
};

ws.onmessage = (event) => {
    const msg = JSON.parse(event.data);
    switch (msg.type) {
        case "connected":
            console.log(`Session: ${msg.sessionId}, Connection: ${msg.connectionId}`);
            break;
        case "content_delta":
            process.stdout.write(msg.delta);
            break;
        case "thinking_delta":
            console.log(`[thinking] ${msg.delta}`);
            break;
        case "tool_start":
            console.log(`[tool] ${msg.toolName} started`);
            break;
        case "tool_end":
            console.log(`[tool] ${msg.toolName} → ${msg.toolResult}`);
            break;
        case "message_end":
            console.log(`\n[done] usage: ${JSON.stringify(msg.usage)}`);
            break;
        case "error":
            console.error(`[error] ${msg.code}: ${msg.message}`);
            break;
    }
};
```

### Sending a Message

```javascript
ws.send(JSON.stringify({ type: "message", content: "Hello, agent!" }));
```

### Steering an Active Run

```javascript
ws.send(JSON.stringify({ type: "steer", content: "Focus on the security implications" }));
```

### Reconnecting After Disconnection

```javascript
// Track the highest sequenceId received
let lastSeqId = 0;
ws.onmessage = (event) => {
    const msg = JSON.parse(event.data);
    if (msg.sequenceId) lastSeqId = msg.sequenceId;
    // ... handle message
};

// On reconnect, replay missed events
const ws2 = new WebSocket("ws://localhost:5005/ws?agent=my-agent&session=my-session");
ws2.onopen = () => {
    ws2.send(JSON.stringify({
        type: "reconnect",
        sessionKey: "my-session",
        lastSeqId: lastSeqId
    }));
};
```

## Dependencies

- **Target framework:** `net10.0`
- **Project references:**
  - `BotNexus.Gateway.Abstractions` — `IChannelAdapter`, `IGatewayWebSocketChannelAdapter`, `IStreamEventChannelAdapter`, message models
  - `BotNexus.Channels.Core` — `ChannelAdapterBase`
- **NuGet packages:** None (uses framework `System.Net.WebSockets`)

## Extension Points

This is a concrete adapter, not a base class. To customize WebSocket behavior:

- Register a `payloadMutator` callback when calling `RegisterConnection` to transform outbound payloads before they are serialized and sent (the Gateway uses this to inject sequence IDs and persist to the replay buffer)
- Implement a custom `IGatewayWebSocketChannelAdapter` for alternative WebSocket handling strategies
- Configure reconnection limits and replay buffer size via `GatewayWebSocketOptions`

## Integration with the Gateway

The adapter works in concert with several Gateway API components:

| Component | Role |
|-----------|------|
| `GatewayWebSocketHandler` | Accepts incoming WebSocket connections, manages lifecycle, registers/unregisters connections with the adapter |
| `WebSocketMessageDispatcher` | Routes inbound messages to the appropriate handler (message, abort, steer, follow-up, reconnect), manages sequence IDs and replay persistence |
| `WebSocketConnectionManager` | Enforces session locking (one connection per session), reconnect throttling with exponential backoff, and ping/pong keepalive |
| `GatewayWebSocketOptions` | Configuration for reconnection limits, backoff timing, and replay buffer size |
