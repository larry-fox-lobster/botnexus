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
