# Orchestration Log — Batch 2 — Farnsworth

**Date:** 2026-04-06T00:40Z  
**Agent:** Farnsworth (Agent Workspace Manager + CLI)  
**Phase:** Gateway Phase 5 — Agent workspace manager + IContextBuilder + BotNexus CLI  
**Model:** gpt-5.3-codex  
**Mode:** background

## Manifest

- **P1 Priority:** Agent workspace manager + IContextBuilder
- **Task 1:** `BotNexusHome` workspace contract + `AgentWorkspaceManager`
- **Task 2:** `IContextBuilder` interface + integration with isolation strategies
- **Task 3:** Gateway API startup provider bootstrap (Auth + Endpoint resolution)
- **Commits:** 42959d9, 52895e7 (2 commits)
- **Status:** Complete

## Deliverables

### Task 1: Agent Workspace Contract
- Added `BotNexusHome` configuration model with required directories: `extensions/`, `tokens/`, `sessions/`, `logs/`, `agents/`
- `GetAgentDirectory(string agentName)` creates and returns `~/.botnexus/agents/{name}`
- First-time workspace scaffolding creates workspace file templates:
  - `SOUL.md` — agent identity and charter
  - `IDENTITY.md` — system-level context
  - `USER.md` — user interaction preferences
  - `MEMORY.md` — session-local memory

### Task 2: Context Builder + Workspace Loading
- `IContextBuilder` interface for composable system message construction
- `AgentWorkspaceContextBuilder` loads SOUL.md, IDENTITY.md, USER.md, MEMORY.md from workspace directory
- Integration with `InProcessIsolationStrategy` to provide workspace context to agent prompts
- Workspace files are optional; missing files result in graceful fallback

### Task 3: Provider Bootstrap + Auth Manager
- `GatewayAuthManager` centralized credential resolution with OAuth support
- Provider bootstrap during `LlmClient` singleton creation in `Program.cs`:
  - `AnthropicProvider`
  - `OpenAICompletionsProvider`
  - `OpenAIResponsesProvider`
  - `OpenAICompatProvider`
- Shared `HttpClient` singleton (10-minute timeout) for provider calls
- Auth resolution chain: `~/.botnexus/auth.json` (with Copilot OAuth) → environment variables → `PlatformConfig`
- `InProcessIsolationStrategy` uses `GatewayAuthManager.GetApiKeyAsync` for runtime key resolution

### Session + Config Lifecycle
- Added `SessionStatus` enum (`Active`, `Suspended`, `Expired`, `Closed`) to `GatewaySession`
- `SessionCleanupService` (`BackgroundService`) with configurable:
  - Check interval: 5 minutes
  - Active session TTL: 24 hours
  - Closed retention: optional (disabled by default)
- Channel capability flags: `SupportsSteering`, `SupportsFollowUp`, `SupportsThinkingDisplay`, `SupportsToolDisplay`
- `PlatformConfigLoader.Watch()` with FileSystemWatcher + 500ms debounce

## Related Decisions

- DEC-2026-04-06-004: Gateway provider/auth bootstrap
- DEC-2026-04-06-005: Gateway session/config lifecycle decisions
  - Capability flags expansion
  - Session lifecycle enforcement
  - BotNexus home agent workspace contract
  - Platform config hot-reload shape

## Notes

- Design review (Leela) flagged P1 items for next sprint:
  - Extract `IGatewayAuthManager` interface (currently concrete)
  - Fix OAuth refresh TOCTOU race (use `SemaphoreSlim` per provider)
  - Split `AddPlatformConfiguration` into focused submethods
- P2 items identified: namespace move for `GatewayAuthManager`, shared path traversal guard utility
