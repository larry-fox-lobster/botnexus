# Farnsworth P1 Gateway Fixes (Phase 9)

## Context
- Requested by Jon Bullen after Leela's Phase 9 design review.
- Scope limited to Gateway API hardening and WebSocket handler decomposition.

## Decisions

1. **PUT `/api/agents/{agentId}` contract hardening**
   - Route/body `AgentId` mismatch now returns `400 Bad Request` with explicit error payload.
   - Empty payload `AgentId` remains supported by normalizing to the route parameter.
   - Added endpoint XML docs + response annotations to make behavior explicit in API surface.

2. **Production CORS verb allowlist**
   - Development keeps permissive CORS for inner-loop productivity.
   - Non-development now explicitly allows `GET, POST, PUT, DELETE, OPTIONS` (instead of `AllowAnyMethod`).
   - Rationale: least-privilege defaults without breaking existing API/WebUI flows.

3. **Gateway WebSocket decomposition**
   - `GatewayWebSocketHandler` now orchestrates only request lifecycle and delegation.
   - `WebSocketConnectionManager` owns reconnect throttling, session lock tracking, duplicate close semantics, and ping/pong handling.
   - `WebSocketMessageDispatcher` owns inbound type routing (`message`, `abort`, `steer`, `follow_up`, `reconnect`) and replay-sequenced outbound persistence.
   - `MapBotNexusGatewayWebSocket` endpoint contract remained unchanged.

## Validation Notes
- Targeted gateway API build passes.
- Full solution build/test runs are currently affected by unrelated workspace churn (outside this change set), including a known integration test failure in `GatewayStartupAndConfigurationTests`.
