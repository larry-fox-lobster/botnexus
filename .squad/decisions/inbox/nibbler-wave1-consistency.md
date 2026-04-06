### Phase 12 Wave 1 — Consistency Review

**Reviewer:** Nibbler
**Date:** 2026-04-06
**Grade:** Good

## Summary

Wave 1 code quality is excellent — all new controllers, DTOs, auth middleware, and tests are well-structured with full XML doc coverage. All issues are documentation gaps: the new endpoints aren't reflected in READMEs and api-reference, and the WebSocket README had a phantom field. 4 P1s fixed directly; 3 P1s flagged for team action.

## Findings

### P1 (Should Fix)

| # | Finding | File(s) | What's Wrong | Fix |
|---|---------|---------|-------------|-----|
| 1 | WebSocket README `error` event had phantom `code` field | `src/channels/BotNexus.Channels.WebSocket/README.md` | README documented `error` event with `message, code` fields but code only sends `message` — `AgentStreamEvent` has no `ErrorCode` property | ✅ Fixed — removed `code` from error event docs |
| 2 | Gateway API README missing new endpoints | `src/gateway/BotNexus.Gateway.Api/README.md` | `/api/channels` and `/api/extensions` not listed in API Endpoints section | ✅ Fixed — added Channels and Extensions endpoint sections |
| 3 | Gateway API README missing new controllers and wrong DTO namespace | `src/gateway/BotNexus.Gateway.Api/README.md` | Controllers table missing `ChannelsController`/`ExtensionsController`; `SessionHistoryResponse` listed as `Controllers` namespace but moved to `Abstractions.Models` | ✅ Fixed — added both controllers, added DTOs, corrected namespace |
| 4 | Auth middleware static file bypass not documented | `src/gateway/BotNexus.Gateway.Api/README.md` | Rewritten auth middleware now skips auth for GET/HEAD requests to actual `wwwroot` files (excluding `/api/**`), but docs only mentioned health/webui/swagger | ✅ Fixed — expanded allowlist documentation with all four bypass categories |
| 5 | `api-reference.md` missing new endpoints | `docs/api-reference.md` | TOC and body have no mention of `GET /api/channels` or `GET /api/extensions` | ⚠️ Needs team action — add Channels and Extensions sections to api-reference.md |
| 6 | `ChannelAdapterResponse.SupportsThinking` naming inconsistency | `src/gateway/BotNexus.Gateway.Api/Controllers/ChannelsController.cs` | All code/interfaces use `SupportsThinkingDisplay` but the API DTO shortens it to `SupportsThinking`. Every other capability flag keeps its exact name (`SupportsStreaming`, `SupportsSteering`, `SupportsFollowUp`, `SupportsToolDisplay`) — only this one diverges. JSON wire format will be `supportsThinking` vs internal `SupportsThinkingDisplay` | ⚠️ Needs team decision — rename DTO param to `SupportsThinkingDisplay` for consistency, or accept the abbreviation |
| 7 | `api-reference.md` still claims query param auth works | `docs/api-reference.md` | Documents `?apiKey=your-api-key-here` but `ApiKeyGatewayAuthHandler` only checks `Headers`, not `QueryParameters`. Phase 11 fix removed this from Gateway.Api README but not api-reference.md | ⚠️ Needs team action — remove query param auth example from api-reference.md |

### P2 (Informational)

| # | Finding | File(s) | What's Wrong | Fix |
|---|---------|---------|-------------|-----|
| 1 | Auth middleware test naming convention split | `GatewayAuthMiddlewareTests.cs` vs `GatewayAuthMiddlewareRuntimeTests.cs` | Main tests use `Scenario_Expected` pattern (e.g., `HealthEndpoint_SkipsAuth`), Runtime tests use `MethodName_Scenario_Expected` (e.g., `InvokeAsync_HealthPath_SkipsAuthentication`). Both are valid but inconsistent within the same Wave | Standardize in future — not blocking |
| 2 | WebSocket README reconnect_ack diagram shows `lastSeqId` as original, not replayed | `src/channels/BotNexus.Channels.WebSocket/README.md` | ASCII diagram shows `reconnect_ack` with `lastSeqId: 42` (the original), which is technically the client's last received ID, not the new last. Semantically correct but could confuse readers expecting the new high-water mark | Clarify in future docs pass |

## Cross-Check Results

| Area | Result | Notes |
|------|--------|-------|
| WebSocket README vs code — message types | ✅ All match | 6 inbound + 10 outbound types verified against `WebSocketChannelAdapter.cs` switch statement |
| WebSocket README vs code — config options | ✅ All match | 5 `GatewayWebSocketOptions` properties match README table exactly (names, types, defaults) |
| WebSocket README vs code — capability flags | ✅ All match | 5 flags (`SupportsStreaming`, `SupportsSteering`, `SupportsFollowUp`, `SupportsThinkingDisplay`, `SupportsToolDisplay`) all `true` in code and README |
| New controllers vs existing patterns | ✅ Consistent | Same `[ApiController]`, `[Route("api/[controller]")]`, `sealed class`, constructor injection, `ActionResult<T>` return types as `AgentsController`/`SessionsController` |
| Controller DI registration | ✅ Auto-discovered | `AddBotNexusGatewayApi()` calls `AddControllers().AddApplicationPart()` — all controllers in the assembly are auto-registered |
| XML doc coverage | ✅ 100% | All new public types: `ChannelsController`, `ExtensionsController`, `ChannelAdapterResponse`, `ExtensionResponse`, `SessionHistoryResponse`, `GatewayAuthMiddleware` |
| Test naming convention | ✅ Consistent (controller tests) | `ChannelsControllerTests` and `ExtensionsControllerTests` follow `MethodName_Scenario_Expected` matching existing `AgentsControllerTests`/`SessionsControllerTests` |
| SessionHistoryResponse location | ✅ Correct | Moved to `Abstractions.Models` — proper placement alongside `GatewaySession`, `SessionEntry` |
| Auth middleware allowlist | ✅ Code correct | `/health`, `/webui/**`, `/swagger/**`, and static web root files all bypass auth as intended |
