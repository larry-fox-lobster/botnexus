### 2026-04-05T19:41Z: Phase 7 Architecture Plan — Gateway Gap Analysis & Sprint Plan
**By:** Leela
**What:** Comprehensive gap analysis of 6 refined Gateway requirements against current codebase, with phased sprint plan for execution.
**Why:** Gap analysis against refined Gateway requirements. The team has completed Phases 1-6 (build green, 225 tests, Grade A). This plan identifies what remains and sequences it for efficient parallel execution.

---

## Methodology

Reviewed every file in `src/gateway/`, `src/channels/`, `src/BotNexus.WebUI/`, `src/gateway/BotNexus.Cli/`, and `tests/BotNexus.Gateway.Tests/`. Cross-referenced against Phase 6 design review findings and team decisions.

---

## Requirement 1: Agent Management

**Status: Partial (75%)**

### What Exists
| Component | File(s) | Status |
|-----------|---------|--------|
| `IAgentRegistry` | Abstractions + `DefaultAgentRegistry.cs` | ✅ Complete |
| `IAgentSupervisor` | Abstractions + `DefaultAgentSupervisor.cs` | ✅ Complete |
| `IAgentCommunicator` | Abstractions + `DefaultAgentCommunicator.cs` | ✅ Complete |
| `IAgentConfigurationSource` | Abstractions + `FileAgentConfigurationSource.cs`, `PlatformConfigAgentSource.cs` | ✅ Complete |
| `AgentDescriptorValidator` | `Agents/AgentDescriptorValidator.cs` | ✅ Complete |
| `IAgentWorkspaceManager` | Abstractions + `FileAgentWorkspaceManager.cs` | ✅ Complete |
| `IContextBuilder` | Abstractions + `WorkspaceContextBuilder.cs` | ✅ Complete |
| Sub-agent calling | `CallSubAgentAsync()` with `::sub::` scoping | ✅ Complete |
| Cross-agent calling | `CallCrossAgentAsync()` with `::cross::` scoping + recursion guard | ✅ Complete |
| Concurrency limits | `MaxConcurrentSessions` enforcement | ✅ Complete |
| Agent lifecycle (start/stop/status) | REST endpoints in `AgentsController` | ✅ Complete |
| Hot-reload config | `AgentConfigurationHostedService` + file watchers | ✅ Complete |

### What's Missing
| Gap | Description | Priority | Effort |
|-----|-------------|----------|--------|
| **Max call chain depth** | No configurable limit on acyclic cross-agent chains (Phase 6 P1-1). 50+ agent chains would exhaust resources. | P1 | S |
| **Cross-agent timeout** | `handle.PromptAsync()` blocks indefinitely if target hangs (Phase 6 P1-3). Need linked `CancellationTokenSource`. | P1 | S |
| **Agent lifecycle events** | No formal events for agent registered/unregistered/config-changed. Needed for UI reactivity and audit. | P2 | S |
| **Agent health checks** | No mechanism to probe agent health or detect faulted instances beyond status enum. | P2 | S |

---

## Requirement 2: Isolation Strategies

**Status: Partial (25% — only in-process works)**

### What Exists
| Component | File(s) | Status |
|-----------|---------|--------|
| `IIsolationStrategy` | Abstractions: `Name`, `CreateAsync()` | ✅ Complete |
| `InProcessIsolationStrategy` | `Isolation/InProcessIsolationStrategy.cs` + `InProcessAgentHandle` | ✅ Complete |
| `SandboxIsolationStrategy` | `Isolation/SandboxIsolationStrategy.cs` | 🔲 Stub (throws `NotSupportedException`) |
| `ContainerIsolationStrategy` | `Isolation/ContainerIsolationStrategy.cs` | 🔲 Stub (throws `NotSupportedException`) |
| `RemoteIsolationStrategy` | `Isolation/RemoteIsolationStrategy.cs` | 🔲 Stub (throws `NotSupportedException`) |
| DI registration | All 4 strategies registered, named resolution works | ✅ Complete |
| `IAgentHandle` | Abstractions: `PromptAsync`, `StreamAsync`, `AbortAsync`, `SteerAsync`, `FollowUpAsync` | ✅ Complete |

### What's Missing
| Gap | Description | Priority | Effort |
|-----|-------------|----------|--------|
| **Sandbox implementation** | Restricted child process with resource limits, filesystem isolation. Phase 2 stub exists. | P2 | M |
| **Container implementation** | Docker container per agent. Phase 2 stub exists. | P2 | L |
| **Remote implementation** | HTTP/gRPC proxy to remote agent instances. Phase 2 stub exists. | P2 | L |
| **Isolation options model** | `AgentDescriptor.IsolationOptions` exists as `Dictionary<string,string>` but no typed options per strategy (e.g., container image, memory limits, sandbox paths). | P1 | S |

**Note:** Per project non-goals, sandbox/container/remote are Phase 2 items. The interface and DI registration are done. Focus this phase on ensuring the in-process strategy is production-solid and the abstraction supports future strategies cleanly. The isolation options model gap is the only P1 here.

---

## Requirement 3: Channel Adapters

**Status: Partial (60%)**

### What Exists
| Component | File(s) | Status |
|-----------|---------|--------|
| `IChannelAdapter` | Abstractions: 8 properties + 4 methods | ✅ Complete |
| `IStreamEventChannelAdapter` | Abstractions: `SendStreamEventAsync` | ✅ Complete |
| `IChannelManager` | Abstractions + `ChannelManager.cs` | ✅ Complete |
| `IChannelDispatcher` | Abstractions: callback for inbound messages | ✅ Complete |
| `ChannelAdapterBase` | `Channels.Core/ChannelAdapterBase.cs` — abstract base with lifecycle | ✅ Complete |
| `WebSocketChannelAdapter` | `Channels.WebSocket/` — full streaming, steering, follow-up, thinking, tools | ✅ Complete |
| `TuiChannelAdapter` | `Channels.Tui/` — streaming + thinking/tool display, console I/O | ✅ Complete |
| `TelegramChannelAdapter` | `Channels.Telegram/` — Phase 2 stub, logs only | 🔲 Stub |
| Capability flags | `SupportsStreaming`, `SupportsSteering`, `SupportsFollowUp`, `SupportsThinkingDisplay`, `SupportsToolDisplay` | ✅ Complete |
| Channel DI extensions | Per-channel `AddBotNexus*Channel()` methods | ✅ Complete |

### What's Missing
| Gap | Description | Priority | Effort |
|-----|-------------|----------|--------|
| **Telegram real implementation** | Stub exists but does nothing. Needs Telegram Bot API integration (polling or webhook), markdown rendering, inline keyboard for steering. | P2 (non-goal: stub rest) | M |
| **Channel steering/queuing** | Requirement says "all channels can use steering and queuing if possible." WebSocket supports it; TUI and Telegram do not. Need steering support in TUI (e.g., `/steer` command) and queue display. | P1 | S |
| **Channel-level message queuing** | No explicit queue abstraction between GatewayHost and adapters. Messages are dispatched directly. For busy agents, inbound messages should queue with backpressure. | P1 | S |
| **Missing channel: WebUI** | WebUI is served as static files via `UseStaticFiles`. It connects via WebSocket but isn't a formal `IChannelAdapter`. This is by design (WebUI is a client of the WebSocket channel), but should be documented. | P2 | S |

---

## Requirement 4: Session Management

**Status: Partial (70%)**

### What Exists
| Component | File(s) | Status |
|-----------|---------|--------|
| `ISessionStore` | Abstractions: `Get`, `GetOrCreate`, `Save`, `Delete`, `List` | ✅ Complete |
| `InMemorySessionStore` | `Sessions/InMemorySessionStore.cs` | ✅ Complete |
| `FileSessionStore` | `Sessions/FileSessionStore.cs` — JSONL + metadata | ✅ Complete |
| `GatewaySession` | Model with history, metadata, status, expiry | ✅ Complete |
| Session status lifecycle | `Active → Suspended → Expired → Closed` | ✅ Complete |
| `SessionCleanupService` | Background service for TTL-based expiry | ✅ Complete |
| Thread-safe history | `ReaderWriterLockSlim` on `GatewaySession` | ✅ Complete |
| Session routing | `DefaultMessageRouter` resolves session → agent binding | ✅ Complete |
| REST session endpoints | `SessionsController`: List, Get, Delete | ✅ Complete |
| WebUI session persistence | localStorage in `app.js` | ✅ Complete |

### What's Missing
| Gap | Description | Priority | Effort |
|-----|-------------|----------|--------|
| **Session reconnection** | No mechanism for a client to reconnect to an existing session after disconnect and resume streaming. WebSocket handler creates new connections but doesn't replay missed events. | P0 | M |
| **Session suspend/resume** | Status enum exists (`Suspended`) but no API to suspend/resume. No `PATCH /api/sessions/{id}/suspend` or `resume` endpoint. | P1 | S |
| **Session metadata API** | Sessions have a `Metadata` dictionary but no REST endpoint to read/write it. Needed for client-side state (e.g., "last seen message"). | P2 | S |
| **Session history pagination** | `GetHistorySnapshot()` returns all entries. For long conversations, need paginated history retrieval (offset/limit). | P1 | S |
| **Database session store** | Only in-memory and file stores exist. For multi-instance deployments, need a database-backed store (SQLite for single-node, with interface ready for Postgres/Redis). | P2 | M |

---

## Requirement 5: API Surface

**Status: Partial (65%)**

### What Exists
| Component | File(s) | Status |
|-----------|---------|--------|
| **REST: Agents** | `AgentsController`: List, Get, Register, Unregister, Instances, StopInstance | ✅ Complete |
| **REST: Chat** | `ChatController`: Send, Steer, FollowUp | ✅ Complete |
| **REST: Sessions** | `SessionsController`: List, Get, Delete | ✅ Complete |
| **REST: Config** | `ConfigController`: Validate | ✅ Complete |
| **REST: Health** | `/health` endpoint | ✅ Complete |
| **WS: Chat** | `GatewayWebSocketHandler`: streaming, steer, follow-up, ping | ✅ Complete |
| **WS: Activity** | `ActivityWebSocketHandler`: agent-filtered activity stream | ✅ Complete |
| **Auth middleware** | `GatewayAuthMiddleware` + `ApiKeyGatewayAuthHandler` | ✅ Complete |
| **Swagger** | Swagger/SwaggerUI configured in `Program.cs` | ✅ Complete |
| **Reconnect protection** | Exponential backoff in `GatewayWebSocketHandler` | ✅ Complete |

### What's Missing
| Gap | Description | Priority | Effort |
|-----|-------------|----------|--------|
| **OpenAPI spec generation** | Swagger is configured but no formal OpenAPI YAML/JSON spec is exported or version-controlled. Requirement says "OpenAPI spec." | P1 | S |
| **REST: Session suspend/resume** | No endpoints for `PATCH /sessions/{id}/suspend` and `/resume` | P1 | S |
| **REST: Session history** | No `GET /sessions/{id}/history?offset=&limit=` for paginated history | P1 | S |
| **REST: Agent configuration update** | Can register/unregister but no `PUT /agents/{id}` for runtime config updates | P1 | S |
| **WS: Session reconnection** | No replay of missed events on reconnect. Need message sequence IDs and replay from last-seen. | P0 | M |
| **WS: Heartbeat improvements** | Client sends `ping` but server doesn't track client liveness. Need server-side ping/pong with timeout for stale connections. | P1 | S |
| **Authorization model** | Auth exists but `AllowedAgents` and `Permissions` are not enforced beyond agent-level access checks. Need role-based permissions (admin vs. user). | P1 | S |
| **Rate limiting** | No request rate limiting on REST or WS endpoints. | P2 | S |
| **Correlation IDs** | No request tracing / correlation ID propagation. | P2 | S |

---

## Requirement 6: Platform Configuration

**Status: Partial (70%)**

### What Exists
| Component | File(s) | Status |
|-----------|---------|--------|
| `PlatformConfig` | `Configuration/PlatformConfig.cs` — Gateway, Agents, Providers, Channels, ApiKeys | ✅ Complete |
| `PlatformConfigLoader` | `Configuration/PlatformConfigLoader.cs` — Load, Validate, Watch | ✅ Complete |
| `BotNexusHome` | `Configuration/BotNexusHome.cs` — `~/.botnexus` path resolution, directory initialization | ✅ Complete |
| Config validation | `PlatformConfigLoader.Validate()` — URI, enum, structural checks | ✅ Complete |
| File-based agent config | `FileAgentConfigurationSource` — hot-reload from JSON files | ✅ Complete |
| Platform-based agent config | `PlatformConfigAgentSource` — agents from `config.json` | ✅ Complete |
| Config watcher | `PlatformConfigWatcher` — debounced file change detection | ✅ Complete |
| CLI validation | `BotNexus.Cli` — `validate --local` and `validate --remote` | ✅ Complete |
| Auth config | `GatewayAuthManager` — auth.json, env vars, OAuth refresh | ✅ Complete |
| Gateway auth config | `ApiKeyConfig` in PlatformConfig with tenant/caller/permissions | ✅ Complete |
| Sample config | `scripts/config.sample.json` | ✅ Complete |

### What's Missing
| Gap | Description | Priority | Effort |
|-----|-------------|----------|--------|
| **Config schema documentation** | No JSON Schema file for `config.json`. Would enable IDE validation and autocomplete. | P1 | S |
| **Config migration/versioning** | No schema version field. When config format changes, no migration path. | P2 | S |
| **CLI: config init** | No `botnexus init` command to scaffold `~/.botnexus/` with default config, agent templates. | P1 | S |
| **CLI: config diff** | No way to compare running config vs. file config. | P2 | S |
| **CLI: agent management** | No `botnexus agents list/add/remove` commands. Only validation exists. | P1 | S |
| **Config: Channel settings** | `ChannelConfig` has `Type`, `Enabled`, `Settings` but settings aren't strongly typed or validated per channel type. | P2 | S |
| **Config: Session store selection** | No config option to choose between InMemory vs. File session store. It's hardcoded in DI registration. | P1 | S |

---

## Summary Matrix

| # | Requirement | Status | Complete% | P0 Gaps | P1 Gaps | P2 Gaps |
|---|-----------|--------|-----------|---------|---------|---------|
| 1 | Agent Management | Partial | 75% | 0 | 2 | 2 |
| 2 | Isolation Strategies | Partial | 25% | 0 | 1 | 3 |
| 3 | Channel Adapters | Partial | 60% | 0 | 2 | 2 |
| 4 | Session Management | Partial | 70% | 1 | 2 | 2 |
| 5 | API Surface | Partial | 65% | 1 | 5 | 2 |
| 6 | Platform Configuration | Partial | 70% | 0 | 4 | 3 |
| **Total** | | | **61%** | **2** | **16** | **14** |

---

## Phased Sprint Plan

### Sprint 7A: Foundation & Reconnection (P0 + critical P1)
**Goal:** Eliminate blockers. Session reconnection and API completeness.

| Work Item | Owner | Effort | Dependencies | Description |
|-----------|-------|--------|--------------|-------------|
| **Session reconnection protocol** | Bender | M | None | Add message sequence IDs to WS stream events. Track last-seen per client. On reconnect, replay missed events from session history. Add `reconnect` WS message type with `lastSeqId`. |
| **Session suspend/resume** | Bender | S | None | Add `PATCH /sessions/{id}/suspend` and `/resume` REST endpoints. Wire to `SessionStatus` transitions. |
| **Session history pagination** | Farnsworth | S | None | Add `offset`/`limit` parameters to `GetHistorySnapshot()` and `GET /sessions/{id}/history` endpoint. |
| **Max call chain depth** | Farnsworth | S | None | Add `MaxCallChainDepth` to `GatewayOptions` (default 10). Enforce in `DefaultAgentCommunicator`. |
| **Cross-agent timeout** | Farnsworth | S | None | Wrap `handle.PromptAsync()` in linked `CancellationTokenSource` with configurable timeout (default 120s). |
| **OpenAPI spec export** | Kif | S | None | Configure `Swashbuckle.AspNetCore.SwaggerGen` to export `openapi.json` at build time. Version-control it. |
| **Config session store selection** | Farnsworth | S | None | Add `SessionStore` config option (`InMemory`/`File`). Wire in DI registration. |
| **Channel steering in TUI** | Bender | S | None | Add `/steer <message>` command to TUI input loop. Set `SupportsSteering = true`. |
| **Channel message queuing** | Bender | S | None | Add bounded `Channel<InboundMessage>` between `IChannelDispatcher` and `GatewayHost` per session. Backpressure when agent is busy. |
| **Sprint 7A tests** | Hermes | M | All above | Tests for reconnection protocol, suspend/resume, pagination, depth limit, timeout, queuing. |
| **Sprint 7A docs** | Kif | S | All above | Update API reference. Document reconnection protocol. Document config options. |

**Parallelizable:** Bender (reconnection + steering + queuing) ‖ Farnsworth (pagination + depth + timeout + session store config) ‖ Kif (OpenAPI + docs). Hermes follows.

**Demo:** Client disconnects and reconnects, resumes streaming from where it left off. TUI supports steering. Session can be suspended and resumed via REST.

---

### Sprint 7B: API Completeness & Configuration (remaining P1)
**Goal:** Full REST/WS API surface. CLI becomes useful management tool. Authorization enforced.

| Work Item | Owner | Effort | Dependencies | Description |
|-----------|-------|--------|--------------|-------------|
| **Agent update endpoint** | Bender | S | None | `PUT /api/agents/{id}` for runtime configuration updates. Validate via `AgentDescriptorValidator`. |
| **Authorization enforcement** | Bender | S | None | Enforce `AllowedAgents` filtering in controllers. Add admin-only gates on register/unregister. Check `Permissions` in middleware. |
| **WS server-side liveness** | Bender | S | Sprint 7A reconnection | Server sends ping, tracks pong. Close stale connections after timeout. |
| **Isolation options model** | Farnsworth | S | None | Define typed options records per strategy (e.g., `SandboxOptions`, `ContainerOptions`). Deserialize from `IsolationOptions` dictionary. |
| **Config JSON Schema** | Farnsworth | S | None | Generate or author JSON Schema for `config.json`. Place in `~/.botnexus/`. |
| **CLI: config init** | Farnsworth | S | None | `botnexus init` command to scaffold `~/.botnexus/` with config template, agent directory, README. |
| **CLI: agent management** | Farnsworth | S | CLI init | `botnexus agents list`, `botnexus agents add <name>`, `botnexus agents remove <name>`. Uses Gateway REST API. |
| **WebUI enhancements** | Fry | M | Sprint 7A reconnection | Reconnection UX showing replay progress. Session suspend/resume buttons. Paginated history loading (infinite scroll). Agent update form. |
| **Sprint 7B tests** | Hermes | M | All above | Auth enforcement tests. CLI command tests. Agent update tests. Liveness tests. |
| **Sprint 7B docs** | Kif | S | All above | CLI usage guide. Authorization model docs. JSON Schema reference. |

**Parallelizable:** Bender (agent update + auth + liveness) ‖ Farnsworth (isolation options + schema + CLI init + CLI agents) ‖ Fry (WebUI) ‖ Kif (docs). Hermes follows.

**Demo:** Full CLI workflow: `botnexus init` → `botnexus validate` → `botnexus agents list`. Authorization blocks unpermitted access. WebUI shows paginated history with reconnection replay.

---

### Sprint 7C: Polish & Hardening (P2 items + carried-forward P1s)
**Goal:** Production readiness for single-node deployment. All P2 enhancements.

| Work Item | Owner | Effort | Dependencies | Description |
|-----------|-------|--------|--------------|-------------|
| **Rate limiting middleware** | Bender | S | None | Per-client rate limiting on REST endpoints. Token bucket or sliding window. |
| **Correlation IDs** | Bender | S | None | Generate `X-Correlation-Id` header. Propagate through middleware, controllers, agents, logs. |
| **Agent lifecycle events** | Farnsworth | S | None | Publish `GatewayActivity` events for agent registered/unregistered/config-changed. |
| **Agent health checks** | Farnsworth | S | None | `IAgentHandle.PingAsync()` or equivalent health probe. Surface in `GET /api/agents/{id}/health`. |
| **Session metadata API** | Farnsworth | S | None | `GET/PATCH /api/sessions/{id}/metadata` for client-side state. |
| **Config migration/versioning** | Farnsworth | S | None | Add `version` field to config. Version-aware loader with migration hooks. |
| **CLI: config diff** | Farnsworth | S | Sprint 7B CLI | Compare running gateway config vs. file config. Show diff. |
| **Channel config validation** | Farnsworth | S | Sprint 7B schema | Validate channel `Settings` per channel type using registered validators. |
| **Database session store** | Bender | M | None | SQLite-backed `ISessionStore` implementation. Use for single-node persistence. |
| **WebUI module splitting** | Fry | M | None | Split `app.js` (now 70.9KB) into modules: connection, chat, agents, sessions, activity. |
| **Telegram real implementation** | Bender | M | None | Telegram Bot API polling, markdown rendering, inline keyboard for steering. (Non-goal but listed for completeness.) |
| **Sprint 7C tests** | Hermes | M | All above | Rate limiting, correlation, health check, SQLite store, lifecycle event tests. |
| **Sprint 7C docs** | Kif | S | All above | Finalize all README files per module. Complete OpenAPI spec. Publish config migration guide. |

**Parallelizable:** Full team parallel. No blocking dependencies within sprint.

**Demo:** End-to-end: rate-limited API, correlation IDs in logs, agent health dashboard, SQLite session persistence surviving restart, modular WebUI.

---

## Carried-Forward Issues (from Phase 5/6 reviews)

These should be addressed during Sprint 7A as quick fixes:

| Issue | Source | Owner | Effort |
|-------|--------|-------|--------|
| `GatewayWebSocketHandler` uses concrete `WebSocketChannelAdapter` (DIP violation) | Phase 5 | Bender | S |
| `Path.HasExtension` potential auth bypass on paths like `/api/agents.json` | Phase 5 | Bender | S |
| `StreamAsync` background task leak (fire-and-forget without tracking) | Phase 5 | Bender | S |

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Session reconnection complexity (replay ordering, race conditions) | High | High | Keep protocol simple: sequence IDs + bounded replay window. Don't attempt full event sourcing. |
| WebUI `app.js` is 70.9KB — becoming unwieldy | Medium | Medium | Sprint 7C splits it. Until then, good section markers exist. |
| Isolation strategy stubs may mislead users | Low | Low | Stubs throw `NotSupportedException` with clear message. Document in config validation. |
| Config schema drift | Medium | Medium | JSON Schema in Sprint 7B catches this early. CI validation in Sprint 7C. |

---

## Architecture Constraints Compliance Check

| Constraint | Status | Notes |
|-----------|--------|-------|
| Fully modular — extension points with clean interfaces | ✅ Met | All 6 concerns have interfaces. Strategies/channels/stores all pluggable. |
| SOLID principles throughout | ✅ Met (4.5/5) | Phase 6 review confirmed. One DIP violation carried forward. |
| API-first — REST/WebSocket primary interface | ✅ Met | All operations exposed via REST. Streaming via WS. |
| Secure by design | ⚠️ Partial | Auth exists but authorization not fully enforced. Sprint 7B closes this. |
| Well documented | ⚠️ Partial | XML docs exist. READMEs partial. OpenAPI spec missing. Sprints 7A-7C close this. |

---

## Recommendation

**Start Sprint 7A immediately.** The two P0 gaps (session reconnection and WS reconnection replay) block any real multi-session client experience. The team can parallelize across 3 tracks:
- **Track 1 (Bender):** Session reconnection + channel improvements
- **Track 2 (Farnsworth):** API gaps + config improvements
- **Track 3 (Kif):** OpenAPI spec + documentation

Hermes and Fry follow once implementations land. Sprint 7A should take 3-5 days with full team.
