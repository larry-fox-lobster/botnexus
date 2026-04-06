---
name: websocket-handler-decomposition
description: Split oversized WebSocket handlers into orchestration, connection, and dispatch components without changing endpoint contracts.
---

# WebSocket Handler Decomposition

## Use When
- A WebSocket handler has accumulated multiple concerns (transport lifecycle, session locking, message routing, replay persistence).
- You need to improve maintainability while preserving endpoint/method contracts.

## Pattern
1. Keep the top-level handler as a thin orchestrator (accept request, validate params, delegate).
2. Extract a **ConnectionManager** for:
   - reconnect throttling
   - active-session lock management
   - duplicate connection close semantics
   - ping/pong keepalive handling
3. Extract a **MessageDispatcher** for:
   - inbound message type routing
   - agent command dispatch (message/abort/steer/follow_up/reconnect)
   - replay sequence persistence + outbound event formatting
4. Preserve endpoint mapping and public handler method signatures to avoid caller churn.
5. Update DI registrations and tests to wire new components explicitly.

## Implementation Checklist
- [ ] Handler `HandleAsync` signature unchanged.
- [ ] New components have XML docs on public classes/methods.
- [ ] Session lock and duplicate-connection behavior unchanged.
- [ ] Replay sequencing and reconnect replay behavior unchanged.
- [ ] Existing websocket endpoint map extension unchanged.
