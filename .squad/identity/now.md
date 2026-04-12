---
updated_at: 2026-04-12T05:00:00Z
focus_area: DDD Refactoring Waves 1-4 Delivered
active_issues: []
status: ddd_refactoring_delivered
---

# What We're Focused On

**DDD Refactoring Waves 1-4 delivered (2026-04-12 05:00Z).** Full domain-driven design alignment: BotNexus.Domain project with value objects + smart enums, SessionStatus Sealed rename, SessionType discrimination, Participants model, ChannelKey + MessageRole + AgentId + SessionId typed across entire stack, sub-agent archetype identity, cron decoupled to IInternalTrigger, existence queries, SessionStoreBase extraction. 6-agent team across 4 waves. 2,043 tests passing, 0 errors, 0 warnings. Deferred phases documented in `docs/planning/ddd-refactoring/deferred-phases.md`.

**Previous:** Multi-session connection model fully delivered (2026-04-11 19:30Z). Fundamental architectural pivot: sessions now pre-warmed at gateway startup, WebUI holds all sessions simultaneously (separate connection per channel/session), switching is pure UI with zero server calls. Eliminated entire class of race conditions. 6 commits, 9 tests, 83/83 E2E passing.

## Current Status

✅ **Sprint:** Phase 12 complete (26+ commits)
✅ **Build:** 0 errors, 0 warnings
✅ **Tests:** 1,015 passing (155 Core + 81 Anthropic + 60 OpenAI + 40 OpenAICompat + 26 Copilot + 71 AgentCore + 146 CodingAgent + 436 Gateway)
✅ **P0s:** 0 open
✅ **Design Reviews:** A- (W1), A- (W2), A (W3)
✅ **Gateway Tests:** 337 → 436 (+99)

## Phase 12 Deliverables

### Wave 1 — Security + Foundation
- Fixed P0 auth bypass (Path.HasExtension → route allowlist)
- Fixed P0 AssemblyPath information disclosure
- Added GET /api/channels and GET /api/extensions endpoints
- Moved SessionHistoryResponse to Abstractions
- WebSocket channel README
- +23 gateway tests

### Wave 2 — Middleware + WebUI Enhancement
- Rate limiting middleware (per-client, configurable)
- Correlation ID middleware
- Session metadata GET/PATCH API
- Config versioning with migration hooks
- WebUI channels panel + extensions panel
- Auth middleware DIP fix (constructor injection)
- SupportsThinkingDisplay naming alignment
- API reference update (all endpoints documented)
- +24 gateway tests

### Wave 3 — Persistence + Documentation
- SQLite session store (Microsoft.Data.Sqlite)
- Agent health check endpoint
- Agent lifecycle events (registered/unregistered/config-changed)
- Session metadata caller authorization
- Rate limiter stale-entry eviction
- WebSocket protocol specification (724 lines)
- Configuration reference guide (676 lines)
- Developer guide update
- +23 gateway tests

## Remaining Backlog (P1/P2)
1. DefaultAgentRegistry.PublishActivity sync-over-async in lock
2. WebUI module splitting (app.js 73KB → ES modules)
3. WebUI model selector
4. Telegram steering support
5. Config diff CLI command
6. E2E integration suite (full gateway lifecycle test)
7. StreamAsync task leak (providers — user review needed)
