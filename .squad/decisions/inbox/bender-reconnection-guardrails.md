# Bender Decision — Reconnection Guardrails for Gateway/WebUI

## Context
Phase 4 design review flagged unbounded reconnect behavior as a P1 reliability risk. Both server and browser could loop indefinitely during outage scenarios, amplifying load and delaying recovery.

## Decision
Enforce reconnection limits at both runtime edges:
1. **Server (Gateway API)** tracks per-client reconnect attempts in a sliding window and rejects excess attempts with HTTP 429 + `Retry-After`.
2. **Client (WebUI)** keeps exponential backoff but now stops after a bounded max retry count and surfaces a manual-reconnect error state.

## Why
Single-sided limits are insufficient: client-only caps can still be bypassed; server-only limits still cause noisy retry storms in the UI. Dual enforcement gives bounded load plus clear UX behavior.

## Files
- `src/gateway/BotNexus.Gateway.Api/WebSocket/GatewayWebSocketHandler.cs`
- `src/gateway/BotNexus.Gateway.Api/WebSocket/GatewayWebSocketOptions.cs`
- `src/BotNexus.WebUI/wwwroot/app.js`
