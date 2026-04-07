# WebSocket Protocol Specification

> Low-level protocol reference for the BotNexus Gateway WebSocket interface.

## Table of Contents

1. [Connection](#connection)
2. [Authentication](#authentication)
3. [Inbound Messages (Client → Server)](#inbound-messages-client--server)
4. [Outbound Messages (Server → Client)](#outbound-messages-server--client)
5. [Sequence ID Tracking](#sequence-id-tracking)
6. [Reconnection Protocol](#reconnection-protocol)
7. [Error Handling](#error-handling)
8. [Session Lifecycle](#session-lifecycle)
9. [Rate Limiting and Backpressure](#rate-limiting-and-backpressure)
10. [Activity Stream WebSocket](#activity-stream-websocket)
11. [Close Codes](#close-codes)

---

## Connection

### Endpoint URL

```
ws://host:port/ws
```

| Parameter   | Required | Description |
|-------------|----------|-------------|
| `agent`     | No       | Optional backward-compatible initial agent selection. |
| `session`   | No       | Optional backward-compatible initial session selection. |

### Handshake

The WebSocket endpoint uses the standard HTTP Upgrade handshake defined by [RFC 6455](https://datatracker.ietf.org/doc/html/rfc6455). The server validates the request before accepting:

1. Verifies the request is a WebSocket upgrade (`Upgrade: websocket` header).
2. Checks rate limits for the client.
3. Accepts the WebSocket and sends a `connected` event.
4. If `agent` was supplied, the server attempts an initial session switch.

**Failure responses before upgrade:**

| Condition | HTTP Status | Body |
|-----------|-------------|------|
| Rate limit exceeded | `429 Too Many Requests` | `Reconnect limit exceeded. Retry in N second(s).` |
| Not a WebSocket request | `400 Bad Request` | — |

The `429` response includes a `Retry-After` header with the number of seconds to wait.

### Session Locking

Each session allows **one active WebSocket connection** at a time. Conflicts are enforced on `switch_session`: if a different connection already owns the target session, the server sends an `error` payload with code `SESSION_ALREADY_CONNECTED` and keeps the socket open.

---

## Authentication

WebSocket connections are subject to the same `GatewayAuthMiddleware` that protects HTTP endpoints.

**Development mode:** When no API keys are configured in `gateway.apiKeys`, connections are allowed without authentication.

**Production mode:** Pass the API key as a query parameter or header on the initial HTTP upgrade request:

```
ws://host:port/ws?agent=assistant&apiKey=sk-my-key
```

Or via header:

```
GET /ws?agent=assistant HTTP/1.1
Upgrade: websocket
X-Api-Key: sk-my-key
```

Auth validation happens during the HTTP upgrade phase — before the WebSocket is accepted. If authentication fails, the server returns an HTTP error (not a WebSocket close frame).

---

## Inbound Messages (Client → Server)

All inbound messages are JSON text frames with a `type` field that determines the message kind.

### `message`

Send a user message to the agent.

```json
{
  "type": "message",
  "content": "What is the capital of France?"
}
```

| Field     | Type   | Required | Description |
|-----------|--------|----------|-------------|
| `type`    | string | Yes      | Must be `"message"` |
| `content` | string | Yes      | The user's message text |

### `switch_session`

Switches the connection's active session without reconnecting.

```json
{
  "type": "switch_session",
  "agentId": "nova",
  "sessionId": "optional",
  "includeHistory": false
}
```

| Field            | Type    | Required | Description |
|------------------|---------|----------|-------------|
| `type`           | string  | Yes      | Must be `"switch_session"` |
| `agentId`        | string  | Yes      | Target agent ID |
| `sessionId`      | string  | No       | Existing session ID. Omit to create a new session ID. |
| `includeHistory` | boolean | No       | When true, server sends `session_history` after switching. |
| `historyLimit`   | int     | No       | Optional max history entries for `session_history` (default 100). |

### `reconnect`

Request replay of missed events after a disconnection. See [Reconnection Protocol](#reconnection-protocol).

```json
{
  "type": "reconnect",
  "sessionKey": "abc123def456",
  "lastSeqId": 42
}
```

| Field        | Type   | Required | Description |
|--------------|--------|----------|-------------|
| `type`       | string | Yes      | Must be `"reconnect"` |
| `sessionKey` | string | No       | Session ID to reconnect to. Defaults to the current session ID if omitted. |
| `lastSeqId`  | long   | No       | Last `sequenceId` the client received. Defaults to `0` (replay all buffered events). |

### `abort`

Cancel the current agent execution.

```json
{
  "type": "abort"
}
```

| Field  | Type   | Required | Description |
|--------|--------|----------|-------------|
| `type` | string | Yes      | Must be `"abort"` |

The server calls `AbortAsync()` on the active agent instance for the session. No-ops if no agent is running.

### `steer`

Inject steering guidance into an active agent run. Steering lets you redirect the agent mid-execution without interrupting the current run.

```json
{
  "type": "steer",
  "content": "Focus on the security implications"
}
```

| Field     | Type   | Required | Description |
|-----------|--------|----------|-------------|
| `type`    | string | Yes      | Must be `"steer"` |
| `content` | string | Yes      | Steering instruction text |

Returns an `error` event with code `SESSION_NOT_FOUND` if no active agent session exists.

### `follow_up`

Queue a follow-up message for the agent's next run. Unlike `steer`, this does not inject into the current run — it queues content for after the current execution completes.

```json
{
  "type": "follow_up",
  "content": "Also explain the performance trade-offs"
}
```

| Field     | Type   | Required | Description |
|-----------|--------|----------|-------------|
| `type`    | string | Yes      | Must be `"follow_up"` |
| `content` | string | Yes      | Follow-up content text |

Returns an `error` event with code `SESSION_NOT_FOUND` if no active agent session exists.

### `ping`

Keepalive ping. The server responds with a `pong` event.

```json
{
  "type": "ping"
}
```

| Field  | Type   | Required | Description |
|--------|--------|----------|-------------|
| `type` | string | Yes      | Must be `"ping"` |

---

## Outbound Messages (Server → Client)

All outbound messages are JSON text frames. Every outbound message includes a `sequenceId` field (see [Sequence ID Tracking](#sequence-id-tracking)).

### `connected`

Sent immediately after the WebSocket is accepted. Confirms the connection and session assignment.

```json
{
  "type": "connected",
  "connectionId": "a1b2c3d4e5f6",
  "sessionId": "session123",
  "agentId": "nova",
  "availableAgents": [{ "agentId": "nova", "displayName": "Nova" }],
  "sequenceId": 1
}
```

| Field          | Type   | Description |
|----------------|--------|-------------|
| `type`         | string | `"connected"` |
| `connectionId` | string | Unique identifier for this WebSocket connection |
| `sessionId`    | string | Optional current session ID when already selected |
| `agentId`      | string | Optional current agent ID when already selected |
| `availableAgents` | array | Registered agents available for switching |
| `sequenceId`   | long   | Monotonically increasing sequence number |

### `session_switched`

Emitted after a successful `switch_session`.

```json
{
  "type": "session_switched",
  "sessionId": "session123",
  "agentId": "nova",
  "connectionId": "a1b2c3d4e5f6",
  "sequenceId": 2
}
```

### `message_start`

Marks the beginning of an agent response.

```json
{
  "type": "message_start",
  "messageId": "msg_abc123",
  "sequenceId": 2
}
```

| Field       | Type   | Description |
|-------------|--------|-------------|
| `type`      | string | `"message_start"` |
| `messageId` | string | Unique identifier for this agent response |
| `sequenceId`| long   | Sequence number |

### `thinking_delta`

Incremental thinking/reasoning content from the agent. Sent when the agent produces internal reasoning (chain-of-thought) that the channel is configured to display.

```json
{
  "type": "thinking_delta",
  "delta": "Let me analyze this step by step...",
  "messageId": "msg_abc123",
  "sequenceId": 3
}
```

| Field       | Type   | Description |
|-------------|--------|-------------|
| `type`      | string | `"thinking_delta"` |
| `delta`     | string | Incremental thinking text fragment |
| `messageId` | string | Parent message ID |
| `sequenceId`| long   | Sequence number |

### `content_delta`

Incremental response content from the agent. This is the primary text output stream.

```json
{
  "type": "content_delta",
  "delta": "The capital of France is ",
  "messageId": "msg_abc123",
  "sequenceId": 4
}
```

| Field       | Type   | Description |
|-------------|--------|-------------|
| `type`      | string | `"content_delta"` |
| `delta`     | string | Incremental content text fragment |
| `messageId` | string | Parent message ID |
| `sequenceId`| long   | Sequence number |

### `tool_start`

The agent has initiated a tool invocation.

```json
{
  "type": "tool_start",
  "toolCallId": "call_xyz789",
  "toolName": "web_search",
  "messageId": "msg_abc123",
  "sequenceId": 5
}
```

| Field        | Type   | Description |
|--------------|--------|-------------|
| `type`       | string | `"tool_start"` |
| `toolCallId` | string | Unique identifier for this tool invocation |
| `toolName`   | string | Name of the tool being called |
| `messageId`  | string | Parent message ID |
| `sequenceId` | long   | Sequence number |

### `tool_end`

A tool invocation has completed.

```json
{
  "type": "tool_end",
  "toolCallId": "call_xyz789",
  "toolName": "web_search",
  "toolResult": "Found 3 results for ...",
  "toolIsError": false,
  "messageId": "msg_abc123",
  "sequenceId": 6
}
```

| Field        | Type   | Description |
|--------------|--------|-------------|
| `type`       | string | `"tool_end"` |
| `toolCallId` | string | Tool invocation identifier (matches `tool_start`) |
| `toolName`   | string | Name of the tool |
| `toolResult` | string | Tool execution result or error message |
| `toolIsError`| bool   | `true` if the tool execution failed |
| `messageId`  | string | Parent message ID |
| `sequenceId` | long   | Sequence number |

### `message_end`

Marks the end of an agent response. Includes token usage statistics.

```json
{
  "type": "message_end",
  "messageId": "msg_abc123",
  "usage": {
    "inputTokens": 150,
    "outputTokens": 42
  },
  "sequenceId": 7
}
```

| Field       | Type   | Description |
|-------------|--------|-------------|
| `type`      | string | `"message_end"` |
| `messageId` | string | Message identifier (matches `message_start`) |
| `usage`     | object | Token usage statistics from the provider |
| `sequenceId`| long   | Sequence number |

### `error`

Error notification. Sent when an error occurs during message processing.

```json
{
  "type": "error",
  "message": "Agent session not found.",
  "code": "SESSION_NOT_FOUND",
  "sequenceId": 8
}
```

| Field       | Type   | Description |
|-------------|--------|-------------|
| `type`      | string | `"error"` |
| `message`   | string | Human-readable error description |
| `code`      | string | Machine-readable error code (see [Error Codes](#error-codes)) |
| `sequenceId`| long   | Sequence number |

### `pong`

Response to a client `ping` message.

```json
{
  "type": "pong",
  "sequenceId": 9
}
```

| Field       | Type   | Description |
|-------------|--------|-------------|
| `type`      | string | `"pong"` |
| `sequenceId`| long   | Sequence number |

### `reconnect_ack`

Confirms a reconnection and reports how many events were replayed. See [Reconnection Protocol](#reconnection-protocol).

```json
{
  "type": "reconnect_ack",
  "sessionKey": "session123",
  "replayed": 3,
  "lastSeqId": 42,
  "sequenceId": 10
}
```

| Field        | Type   | Description |
|--------------|--------|-------------|
| `type`       | string | `"reconnect_ack"` |
| `sessionKey` | string | Session that was reconnected |
| `replayed`   | int    | Number of events replayed to the client |
| `lastSeqId`  | long   | The `lastSeqId` the client reported (echo) |
| `sequenceId` | long   | Sequence number |

---

## Sequence ID Tracking

Every outbound message from the server includes a monotonically increasing `sequenceId` field. Sequence IDs are scoped to a session and persist across reconnections.

### How It Works

1. Each `GatewaySession` maintains a replay buffer that allocates sequence IDs.
2. When the server sends any outbound event, `SequenceAndPersistPayloadAsync` is called:
   - Allocates the next `sequenceId` from the session's replay buffer.
   - Injects `sequenceId` into the JSON payload.
   - Stores the serialized event in the replay buffer (capped at `ReplayWindowSize`).
   - Persists the session state to the session store.
3. The replay buffer retains the most recent events (default: 1000) for reconnection replay.

### Client Tracking

Clients should track the highest `sequenceId` received:

```javascript
let lastSeqId = 0;

ws.onmessage = (event) => {
    const msg = JSON.parse(event.data);
    if (msg.sequenceId) {
        lastSeqId = msg.sequenceId;
    }
    // ... handle message
};
```

This value is sent back to the server during reconnection to identify the gap.

---

## Reconnection Protocol

The protocol supports transparent reconnection with event replay to recover from network interruptions.

### Flow

```
Client                              Server
  │                                   │
  │  (connection lost)                │
  │                                   │
  │── new WebSocket connection ──────►│  ws://host/ws?agent=X&session=Y
  │                                   │
  │◄── connected ────────────────────│  { type: "connected", sequenceId: N }
  │                                   │
  │── reconnect ─────────────────────►│  { type: "reconnect", sessionKey: "Y", lastSeqId: 42 }
  │                                   │
  │◄── replayed event (seqId: 43) ───│  (original payload, verbatim)
  │◄── replayed event (seqId: 44) ───│
  │◄── replayed event (seqId: 45) ───│
  │                                   │
  │◄── reconnect_ack ───────────────│  { type: "reconnect_ack", replayed: 3, lastSeqId: 42 }
  │                                   │
  │  (normal messaging resumes)       │
  │                                   │
```

### Steps

1. **Client opens a new WebSocket** to the same endpoint with the same `session` parameter.
2. **Server sends `connected`** event with a new `connectionId`.
3. **Client sends `reconnect`** with its `sessionKey` (the session ID) and `lastSeqId` (the highest sequence ID it received before disconnection).
4. **Server replays missed events** — all buffered events with `sequenceId > lastSeqId` are sent verbatim in order.
5. **Server sends `reconnect_ack`** confirming the replay count and the client's `lastSeqId`.
6. **Normal messaging resumes** from the new connection.

### Replay Buffer

The server retains the most recent outbound events per session in a bounded replay buffer:

| Setting | Default | Description |
|---------|---------|-------------|
| `ReplayWindowSize` | `1000` | Maximum number of events retained for replay |

Events beyond the window size are evicted (oldest first). If the client's `lastSeqId` is older than the oldest buffered event, only the available buffered events are replayed — no error is raised, but a gap may exist.

### Session Validation

During reconnection, the server validates:
- The session exists in the session store.
- The session's `agentId` matches the `agent` parameter from the WebSocket URL.

If validation fails, the server sends an `error` event with code `SESSION_NOT_FOUND`.

---

## Error Handling

### Error Event Format

All errors are delivered as `error` type outbound events with a `message` and `code`:

```json
{
  "type": "error",
  "message": "Human-readable error description",
  "code": "ERROR_CODE",
  "sequenceId": 42
}
```

### Error Codes

| Code | Trigger | Description |
|------|---------|-------------|
| `SESSION_NOT_FOUND` | `steer`, `follow_up`, or `reconnect` when no active session exists | The target agent session does not exist or the agent ID does not match |
| `AGENT_ERROR` | Exception during `message` processing | An unhandled error occurred while dispatching the user message to the agent |

### Connection-Level Errors

Some errors occur at the HTTP level, before the WebSocket is established:

| HTTP Status | Cause |
|-------------|-------|
| `400` | Not a WebSocket request, or missing `agent` query parameter |
| `401` / `403` | Authentication failure (production mode) |
| `429` | Rate limit exceeded — includes `Retry-After` header |

### Socket Close Errors

| Close Code | Meaning |
|------------|---------|
| `4409` | Session already has an active WebSocket connection (duplicate) |
| `1000` | Normal closure (activity stream graceful shutdown) |

---

## Session Lifecycle

A typical session progresses through these states:

```
┌────────────┐     ┌────────────────┐     ┌────────────┐     ┌────────────┐
│  Connect   │────►│  Messaging     │────►│  Streaming  │────►│  Idle      │
│            │     │                │     │  Response   │     │            │
└────────────┘     └────────────────┘     └────────────┘     └────────────┘
     │                    │                     │                   │
     │ connected          │ message             │ message_start     │ (await next
     │ event              │ (inbound)           │ content_delta*    │  message)
     │                    │                     │ tool_start/end*   │
     │                    │                     │ thinking_delta*   │
     │                    │                     │ message_end       │
     │                    │                     │                   │
     │                    │              ┌──────┴──────┐            │
     │                    │              │   Abort     │            │
     │                    │              │  (client    │            │
     │                    │              │   sends     │            │
     │                    │              │   abort)    │            │
     │                    │              └─────────────┘            │
     │                    │                                        │
     │              ┌─────┴──────┐                           ┌─────┴──────┐
     │              │  Steer     │                           │  Reconnect │
     │              │  (mid-run  │                           │  (after    │
     │              │   inject)  │                           │   drop)    │
     │              └────────────┘                           └────────────┘
```

### Phase Details

| Phase | Description |
|-------|-------------|
| **Connect** | Client opens WebSocket. Server validates, reserves session slot, sends `connected`. |
| **Messaging** | Client sends `message`. Server dispatches to agent via the channel adapter. |
| **Streaming Response** | Agent produces events: `message_start` → (`thinking_delta`\|`content_delta`\|`tool_start`\|`tool_end`)* → `message_end`. |
| **Idle** | Agent response complete. Client can send another `message` or disconnect. |
| **Abort** | Client sends `abort` during a streaming response. Agent execution is cancelled. |
| **Steer** | Client sends `steer` during a streaming response. Guidance is injected into the active run. |
| **Follow-up** | Client sends `follow_up` during or after a run. Content is queued for the next execution. |
| **Reconnect** | Client reconnects after a disconnection. Missed events are replayed. |

### Disconnect Cleanup

When a WebSocket disconnects (graceful close, network drop, or cancellation):

1. The connection is unregistered from the channel adapter.
2. The session slot is released, allowing future connections.
3. The session and its replay buffer remain in the session store for reconnection.

---

## Rate Limiting and Backpressure

### Connection Rate Limiting

The server enforces connection attempt throttling per client IP + agent ID pair to prevent reconnect storms.

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxReconnectAttempts` | `20` | Maximum connection attempts within the tracking window |
| `AttemptWindowSeconds` | `300` | Sliding window duration (5 minutes) |
| `BackoffBaseSeconds` | `1` | Base retry delay after hitting the limit |
| `BackoffMaxSeconds` | `60` | Maximum retry delay (exponential backoff cap) |

### Throttling Behavior

1. Each connection attempt from a client IP + agent pair increments a counter within a sliding window.
2. If the counter reaches `MaxReconnectAttempts`, subsequent attempts receive HTTP `429` with a `Retry-After` header.
3. The retry delay uses exponential backoff: `min(BackoffBaseSeconds × 2^(attempt-1), BackoffMaxSeconds)`.
4. Once the sliding window expires, the counter resets.

### Client Key

The rate limiter identifies clients using:
- `X-Forwarded-For` header (first IP, for proxied connections)
- `RemoteIpAddress` (fallback for direct connections)
- Combined with the `agentId` to form the throttle key: `{clientIp}::{agentId}`

### Stale Window Cleanup

The server periodically cleans up expired tracking windows. Cleanup runs every 128 connection attempts to avoid per-request overhead.

### HTTP Rate Limiting

Separate from WebSocket connection throttling, the Gateway supports HTTP request rate limiting configured via `gateway.rateLimit`:

| Setting | Default | Description |
|---------|---------|-------------|
| `requestsPerMinute` | `60` | Maximum requests per client per window |
| `windowSeconds` | `60` | Rate limit window duration |

---

## Activity Stream WebSocket

A read-only WebSocket endpoint for monitoring gateway activity events in real time.

### Endpoint

```
ws://host:port/ws/activity?agent={agentId}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `agent`   | No       | Filter events to a specific agent. When omitted, all events are streamed. |

### Behavior

- This is a **server-push-only** connection. The server streams events; no client messages are expected.
- Events are serialized as JSON text frames using camelCase property names.
- The connection closes gracefully with close code `1000` when the server shuts down.

### Event Schema

Each event is a `GatewayActivity` record:

```json
{
  "eventId": "a1b2c3d4e5f6",
  "type": "MessageReceived",
  "agentId": "assistant",
  "sessionId": "session123",
  "channelType": "websocket",
  "message": "Received message from user",
  "timestamp": "2026-04-06T12:00:00.000Z",
  "data": {
    "contentLength": 42
  }
}
```

| Field        | Type    | Description |
|--------------|---------|-------------|
| `eventId`    | string  | Unique event identifier |
| `type`       | string  | Activity type (see below) |
| `agentId`    | string? | Agent involved, if any |
| `sessionId`  | string? | Session involved, if any |
| `channelType`| string? | Channel type (e.g., `"websocket"`) |
| `message`    | string? | Human-readable event summary |
| `timestamp`  | string  | ISO 8601 UTC timestamp |
| `data`       | object? | Extensible payload with event-specific fields |

### Activity Types

| Type | Description |
|------|-------------|
| `MessageReceived` | A message was received from a channel |
| `ResponseSent` | A response was sent to a channel |
| `StreamDelta` | A streaming delta was sent to a channel |
| `AgentProcessing` | An agent started processing a request |
| `AgentCompleted` | An agent completed processing |
| `ToolExecutionStarted` | A tool execution started |
| `ToolExecutionCompleted` | A tool execution completed |
| `AgentStarted` | An agent instance was created |
| `AgentStopped` | An agent instance was stopped |
| `SessionCreated` | A session was created |
| `Error` | An error occurred |
| `System` | A system-level informational event |

### Differences from `/ws`

| Feature | `/ws` (Agent) | `/ws/activity` (Monitor) |
|---------|---------------|--------------------------|
| Direction | Bidirectional | Server → client only |
| Sequence IDs | Yes | No |
| Reconnection replay | Yes | No |
| Session scoped | Yes | No (gateway-wide) |
| Rate limited | Yes | No |
| Authentication | Yes | Yes |

---

## Close Codes

| Code | Name | Description |
|------|------|-------------|
| `1000` | Normal Closure | Graceful disconnect (activity stream shutdown) |
| `1001` | Going Away | Server shutting down or client navigating away |
| `4409` | Session Conflict | Session already has an active connection; duplicate rejected |

---

## See Also

- [API Reference](api-reference.md) — REST endpoint documentation and WebSocket overview
- [WebSocket Channel README](../src/channels/BotNexus.Channels.WebSocket/README.md) — Adapter implementation details
- [Configuration Guide](configuration.md) — Platform configuration including WebSocket options
- [Developer Guide](dev-guide.md) — Running the Gateway locally
