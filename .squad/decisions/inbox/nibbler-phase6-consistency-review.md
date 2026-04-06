# Phase 6 Consistency Review

**Reviewer:** Nibbler  
**Date:** 2026-07-18  
**Scope:** Phase 6 delivery — 5 commits, cross-agent calling, channel capabilities, WebUI enhancements, dev scripts, developer docs  
**Verdict:** ✅ **Good** — No blocking issues after fixes applied.

---

## Summary

| Severity | Found | Fixed | Remaining |
|----------|-------|-------|-----------|
| **P0** | 4 | 4 | 0 |
| **P1** | 5 | 5 | 0 |
| **P2** | 5 | 0 | 5 |
| **P3** | 1 | 0 | 1 |

Phase 6 code quality is high — cross-agent calling, channel capability flags, and test assertions all consistent with implementations. The issues cluster entirely in **documentation**, specifically `docs/api-reference.md` which was the most stale artifact in the project. Several sections predated the current `PlatformConfig` schema and port defaults.

---

## P0 — Fixed (Broken)

### 1. api-reference.md: Wrong default port (18790 → 5005)
**File:** `docs/api-reference.md` line 19  
**Issue:** Base URL documented as `http://localhost:18790/api` but every other doc, every script, and the code all use port **5005**. Curl example on line 776 also used 18790.  
**Fix:** Updated to `http://localhost:5005/api`. Fixed curl example.

### 2. api-reference.md: Missing Chat endpoints
**File:** `docs/api-reference.md`  
**Issue:** `POST /api/chat`, `POST /api/chat/steer`, and `POST /api/chat/follow-up` exist in `ChatController.cs` and are listed in the README, but were completely absent from the API reference.  
**Fix:** Added full Chat section with all 3 endpoints, request/response shapes matching actual `ChatRequest`/`ChatResponse`/`AgentControlRequest` records.

### 3. api-reference.md: Sessions API documented non-existent endpoints
**File:** `docs/api-reference.md`  
**Issue:** Documented `PATCH /api/sessions/{key}` (hide/unhide) and `hidden` query parameter, but `SessionsController` only has: `GET /api/sessions` (with `agentId` filter), `GET /api/sessions/{sessionId}`, and `DELETE /api/sessions/{sessionId}`.  
**Fix:** Replaced with actual endpoints. Added GET single session and DELETE session.

### 4. README.md: Stale "Minimal Configuration" example
**File:** `README.md` lines 191-212  
**Issue:** Used old PascalCase config schema (`BotNexus.Agents.Model`, `BotNexus.Gateway.Host/Port`) with port **18790**. The current schema uses lowercase keys (`gateway`, `agents`, `providers`) and port 5005.  
**Fix:** Replaced with current PlatformConfig-compatible example.

---

## P1 — Fixed (Inconsistent)

### 5. api-reference.md: Auth exemptions incomplete and wrong
**File:** `docs/api-reference.md` line 44  
**Issue:** Said only `/health` and `/ready` are auth-exempt. Actual `GatewayAuthMiddleware.IsExemptPath()` exempts `/health`, `/webui`, `/swagger`. The `/ready` endpoint doesn't exist in code.  
**Fix:** Updated to list actual exempt paths. Removed non-existent `/ready` endpoint section.

### 6. README.md: WebSocket protocol missing `thinking_delta`
**File:** `README.md` WebSocket Protocol section  
**Issue:** Server → Client message types listed 8 types but omitted `thinking_delta`. The `GatewayWebSocketHandler` XML docs and `app.js` both handle this message type.  
**Fix:** Added `thinking_delta` to the protocol documentation.

### 7. api-reference.md: Agent response shape mismatched actual `AgentDescriptor`
**File:** `docs/api-reference.md` agent endpoint responses  
**Issue:** Documented agents with fields `name`, `systemPrompt`, `model`, `provider`, `maxTokens`, `temperature`, `disallowedTools`, `disabledSkills`. Actual `AgentDescriptor` record uses `agentId`, `displayName`, `modelId`, `apiProvider`, `isolationStrategy`, `toolIds`, `subAgentIds`, `maxConcurrentSessions`.  
**Fix:** Updated all agent response examples and Create Agent request to match `AgentDescriptor`.

### 8. Copilot auth reference naming inconsistent across docs
**Files:** `docs/sample-config.json`, `README.md`, `docs/dev-guide.md`  
**Issue:** `dev-guide.md` used `"auth:copilot"` and `api.githubcopilot.com`. `sample-config.json` used `"auth:github-copilot"` and `api.enterprise.githubcopilot.com`. `README.md` mixed both.  
**Fix:** Standardized to `"auth:copilot"` and `"https://api.githubcopilot.com"` (matching dev-guide, the most comprehensive reference).

### 9. api-reference.md: Curl example used old agent field name
**File:** `docs/api-reference.md` Disabling Tools section  
**Issue:** Curl example used `"name": "secure-agent"` but actual API expects `"agentId"`.  
**Fix:** Updated to use current `AgentDescriptor` fields.

---

## P2 — Report Only (Stale)

### 10. dev-guide.md missing `-SkipBuild` and `-SkipTests` parameters
**Files:** `docs/dev-guide.md`, `scripts/start-gateway.ps1`, `scripts/dev-loop.ps1`  
**Detail:** Both scripts have `-SkipBuild` parameters (and dev-loop has `-SkipTests`) that aren't documented in the dev guide's Scripts Reference tables.  
**Impact:** Low — users can discover these via `Get-Help`.

### 11. api-reference.md: Stale PascalCase config examples in Tools and Loop Detection sections
**File:** `docs/api-reference.md` lines ~760, ~915  
**Detail:** Config examples still use old `"BotNexus": { "Agents": { "Named": { ... } } }` format instead of current `{ "agents": { ... } }`.  
**Impact:** Medium — misleading for anyone trying to configure tool disabling or loop detection via config.

### 12. README.md: Sessions directory path inconsistency
**File:** `README.md` line 187  
**Detail:** Home directory layout shows `workspace/sessions/` but dev-guide and gateway README show `sessions/`. The PlatformConfig `sessionsDirectory` field would depend on what's configured.  
**Impact:** Low — illustrative only, actual path is config-driven.

### 13. README.md: Project structure lists `BotNexus.Api` as separate project
**File:** `README.md` line 279  
**Detail:** Shows `BotNexus.Api` in the project structure tree. This may have been merged into Gateway.Api or renamed.

### 14. `ConfigureAwait(false)` inconsistency persists in Gateway library
**Detail:** Carried forward from Phase 4/5. `AgentConfigurationHostedService` uses `ConfigureAwait(false)`, other Gateway library code doesn't. Not introduced in Phase 6.

---

## P3 — Report Only (Nit)

### 15. README.md: Configuration layering order presentation
**File:** `README.md` vs `docs/dev-guide.md`  
**Detail:** README lists layering bottom-up (1. Code defaults → 4. Env vars), dev-guide lists top-down (1. Env vars → 4. Code defaults). Both are correct but the opposite ordering could confuse developers.

---

## Checks Passed ✅

| Check | Result |
|-------|--------|
| **Docs ↔ Docs** — Port numbers, config structure, startup flow | ✅ Aligned after fixes |
| **Docs ↔ Code** — API endpoints, response shapes, defaults | ✅ Aligned after fixes |
| **Code ↔ Comments** — XML docs on DefaultAgentCommunicator, channels | ✅ Accurate |
| **README ↔ Reality** — WS protocol, config schema, project description | ✅ Aligned after fixes |
| **Config ↔ Code** — sample-config.json ↔ PlatformConfig.cs | ✅ All keys match |
| **WebUI ↔ Server** — app.js message types ↔ GatewayWebSocketHandler | ✅ Consistent |
| **Cross-agent calling ↔ Tests** — Session IDs, exceptions, concurrency | ✅ All assertions match |
| **Channel capabilities ↔ IChannelAdapter** — TUI and Telegram flags | ✅ Interface fully implemented |
| **Build** — `dotnet build BotNexus.slnx` | ✅ 0 errors, 25 warnings (pre-existing) |

---

## Pattern Observations

1. **api-reference.md was the staleness hotspot.** It predated several config schema changes and the Phase 5-6 controller additions. Going forward, any new controller endpoint should trigger an api-reference.md update as a checklist item.

2. **Auth reference naming** (`auth:copilot` vs `auth:github-copilot`) drifted because multiple agents authored different docs in parallel. A canonical "copy-paste config snippet" in a shared location would prevent this.

3. **WebSocket protocol documentation** is split across README.md, gateway README, and GatewayWebSocketHandler XML docs. The handler's XML docs are the source of truth and are comprehensive — other locations should reference them or be auto-generated.
