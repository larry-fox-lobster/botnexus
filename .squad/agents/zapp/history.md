# Project Context

- **Owner:** Jon Bullen
- **Project:** BotNexus — modular AI agent execution platform (OpenClaw-like) built in C#/.NET. Lean core with extension points for assembly-based plugins. Multiple agent execution modes. SOLID patterns. Comprehensive testing.
- **Stack:** C# (.NET latest), modular class libraries, dynamic extension loading, Copilot provider with OAuth
- **Created:** 2026-04-01

## Learnings

- 2026-04-01: Added to team to own E2E simulation and deployment lifecycle testing. Split from Hermes who keeps unit + integration tests. Hermes tests code quality; Zapp tests customer experience.
- Existing E2E: 15 tests with 5 agents (Nova/Quill/Bolt/Echo/Sage), 2 mock channels, in-process via WebApplicationFactory. Needs expansion to cover workspace/memory features and deployment lifecycle.
- Deployment lifecycle tests need real process starts (dotnet run), not just in-process. Must cover: install, configure, start, stop, restart, update, health probes, session persistence across restarts.
- Created tests/SCENARIOS.md — the E2E scenario registry. 56 scenarios across 8 categories. 38 covered (68%), 2 partial (4%), 16 planned (28%). Full audit of all 124+ tests across E2E, Integration, and Unit projects. Each scenario has ID, status, test location, description, and steps. Appendix maps every test file to its scenario IDs. Biggest gap: Deployment Lifecycle (10 planned, 0 covered) — needs real process-level testing infrastructure.
- Implemented all 10 deployment lifecycle E2E tests (SC-DPL-001 through SC-DPL-010) in `tests/BotNexus.Tests.Deployment/`. Real process testing — Gateway started via `dotnet <dll>` with isolated temp BOTNEXUS_HOME per test. All 10 pass. Coverage now 48/56 (86%).
- Key infrastructure: `GatewayProcessFixture` manages process lifecycle — starts Gateway as an OS process, polls /health, kills on cleanup. Uses random ports, isolated temp dirs, `await using` pattern for guaranteed process cleanup.
- Discovered: SessionManager path = `{workspace}/sessions/` (from `config.Agents.Workspace`), NOT `{BOTNEXUS_HOME}/sessions/`. The `sessions/` dir in home is created by Initialize() but not used by SessionManager. Fixed by setting `Workspace: "~/.botnexus"` in test config.
- Discovered: Agent workspaces are lazy-created on first message, not at Gateway startup. `InitializeAgentWorkspace` is called by `AgentContextBuilder.BuildSystemPromptAsync()` during message processing. Cannot test workspace creation without a working agent runner.
- Discovered: Extension loader does NOT auto-scan folders. Extensions must be explicitly configured in `config.Providers`, `config.Channels.Instances`, or `config.Tools.Extensions`. The keys in those dicts determine which `{type}/{key}/` folder is scanned.
- Discovered: xUnit 2.9.x with runner 3.1.4 does NOT reliably call `IAsyncDisposable.DisposeAsync()` on test class instances. Must use `await using var fixture = ...` inside test methods for guaranteed process cleanup. Without this, child processes are orphaned.
- Discovered: `Process.Kill(entireProcessTree: true)` is required on Windows. `Kill(false)` may leave orphaned child processes from the dotnet host.


### 2026-04-02 — Sprint 5 Complete: Agent Workspace, Memory, Deployment Lifecycle

**Overview:** Sprint 5 delivered the core agent infrastructure (workspace + identity), memory management system (long-term + daily with consolidation), and comprehensive deployment lifecycle validation (10 real-process E2E scenarios).

**Achievement:** 48/50 items done. 2 P2 items deferred (Anthropic tool-calling, plugin architecture deep-dive). Team grew from 6 to 8 agents (Nibbler + Zapp added).

**Workspace & Identity (Leela ws-01/02, Farnsworth ws-03/04/05):**
- Agent workspace structure: ~/.botnexus/agents/{agent-name}/ with SOUL/IDENTITY/USER/AGENTS/TOOLS/MEMORY files
- BotNexusHome.Initialize() creates workspace structure and stub files
- Multi-agent awareness via auto-generated AGENTS.md (from config + identity files)
- File-based persistent identity and personality system
- Integration tests for workspace creation, file structure, and initialization

**Context Builder & Memory Services (Bender ws-06 through ws-12, Farnsworth ws-13):**
- IContextBuilder interface replaces flat systemPrompt with file-driven context assembly
- Context loads workspace files (SOUL, IDENTITY, USER, AGENTS, TOOLS, MEMORY) at session start
- Memory tools added: memory_search (FTS), memory_save, memory_get, memory_list
- Daily memory files (~/.botnexus/agents/{name}/memory/YYYY-MM-DD.md) auto-loaded for today + yesterday
- Long-term MEMORY.md consolidation via LLM-based distillation
- Token budget trimming integrated into context builder

**Heartbeat & Memory Consolidation (Bender ws-15, Farnsworth ws-16):**
- IHeartbeatService runs daily consolidation job: distills daily files → MEMORY.md
- Controlled pruning prevents unbounded memory growth
- Health check integrated with heartbeat service

**Deployment Lifecycle Testing (Hermes ws-17 through ws-21):**
- Implemented 10 real-process E2E scenarios in tests/BotNexus.Tests.Deployment/
- GatewayProcessFixture: spawns Gateway via dotnet run with isolated temp dirs, health probes
- Scenarios cover: install, config creation, startup, agent workspace setup, message routing, multi-agent handoff, session persistence, graceful shutdown, restart with session restoration, platform update, health management, OAuth integration
- All 10 pass. Scenario registry now 86% coverage (48/56 total scenarios).
- Key discovery: Sessions persisted across restart; workspace creation is lazy (on first message); extension loading is explicit, not auto-scanning.

**Scenario Registry & Team Expansion (Leela ws-22, Zapp scenario-registry + deployment-lifecycle-tests):**
- Zapp added to team: owns E2E deployment validation, deployment lifecycle tests
- Nibbler added to team: owns consistency reviews, post-sprint audits
- Scenario registry process formalized: Hermes maintains as living document after sprint completion
- Consistency review ceremony established: triggered after sprint or architecture changes

**Process Updates:**
- All decisions from inbox merged into decisions.md (decisions #9, #10, #11)
- Inbox files deleted (merged, not orphaned)
- Cross-agent consistency checks now a formal ceremony with Nibbler as owner
- Documentation updated and consistency audit completed (Leela: 22 issues fixed across 5 files)

**Outstanding:**
- 2 P2 items deferred to next sprint: Anthropic tool-calling feature parity, plugin architecture deep-dive
- Hearbeat service still needs HealthCheck.AggregateAsync() implementation (minor gap)
- Plugin discovery (AssemblyLoadContext per extension) not yet fully tested with real extension deployments
