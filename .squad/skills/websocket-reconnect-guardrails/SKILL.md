# Skill: websocket-reconnect-guardrails

## When to use
Use this pattern whenever BotNexus introduces or updates persistent WebSocket clients/servers (Gateway API, WebUI, channel adapters).

## Pattern
1. Apply **server-side attempt accounting** per client identity and endpoint target.
2. Enforce a **sliding-window max attempts** limit.
3. On limit breach, return **429 Too Many Requests** with `Retry-After`.
4. On client, implement **exponential backoff + max retries**.
5. After max retries, stop auto-retry and show an actionable manual reconnect message.

## BotNexus reference implementation
- Server: `src/gateway/BotNexus.Gateway.Api/WebSocket/GatewayWebSocketHandler.cs`
- Server options: `src/gateway/BotNexus.Gateway.Api/WebSocket/GatewayWebSocketOptions.cs`
- Client: `src/BotNexus.WebUI/wwwroot/app.js`

## Guardrails
- Keep counters bounded (periodic stale-entry cleanup).
- Key by client identity that survives reconnects (IP / forwarded-for + agent scope).
- Keep retry limits configurable via options.
