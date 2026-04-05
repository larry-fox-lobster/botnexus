# Sprint 7 Complete — CLI, Doctor, Hot Reload

**Date:** 2026-04-02T00:34Z  
**Coordinator:** Jon Bullen  
**Team:** Leela (Architect), Bender (Core), Fry (Gateway), Ralph (Tests), Hermes (Config), Zapp (Orchestration), Kif (Documentation)  

---

## Deliverables

### CLI Tool (botnexus command)
- **16 commands** via System.CommandLine framework
- **Command groups:** start/stop/restart/status/logs/shutdown, config (validate/show/init), agent/provider/channel/extension/logs
- **Infrastructure:** GatewayClient (REST), ConfigFileManager (file I/O), ConsoleOutput (formatting)
- **First release:** Installed as .NET tool, manages BotNexus processes

### Doctor Diagnostics
- **13 checkups** across 6 categories: config, security, connectivity, extensions, providers, permissions, resources
- **Pluggable system:** IHealthCheckup interface, CheckupRunner orchestrator
- **Auto-fix support:** Optional FixAsync() with CanAutoFix property
- **CLI modes:** diagnose-only, interactive fix (--fix), force all (--fix --force)
- **REST endpoint:** /api/doctor for remote diagnostics

### Config Hot Reload
- **ConfigReloadOrchestrator:** Coordinates reload of Options<T>, logging, providers, extensions
- **IOptionsMonitor:** .NET standard approach, watches ~/.botnexus/config.json
- **FileSystemWatcher:** Detects file changes, triggers reload cycle
- **IHostedService:** Gateway integration, runs on startup

### Gateway REST Endpoints
- **/api/status** — System health, provider registration, extension loader status
- **/api/doctor** — Trigger diagnostics, return checkup results with fix availability
- **/api/shutdown** — Graceful shutdown with cleanup

### Test Suite
- **443 tests passing:** 322 unit + 98 integration + 23 E2E
- **Coverage areas:** CLI commands, doctor checkups, config reload, gateway endpoints, extension loading
- **First-run scenario:** Follows getting-started guide, validates end-to-end flow

---

## P0 Bug Fixed

**Issue:** Gateway unhealthy on first run (extension loader failure)  
**Root cause:** Extension assembly validation + config reference mismatches  
**Solution:** Auto-scan extensions/ folder, validate assemblies, lazy-load only configured providers  
**Result:** First-run scenario now passes; /api/status returns Healthy

---

## Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| CLI: System.CommandLine | Standard .NET tool pattern, built-in options parsing, verb hierarchy |
| Doctor: Pluggable interface | Extensible for future checks, independent of CLI/Gateway |
| Auto-fix: Interactive + Force | User control — diagnose first, ask before fixing, or force all |
| Config reload: IOptionsMonitor | .NET DI standard, works with Options<T>, scoped to re-registrations |
| Extension loader: Auto-scan | No manual registration needed, detects new extensions on reload |
| Logging: ~/.botnexus/logs | Consistent with BotNexusHome, persistent across runs |

---

## Team Notes

- **Kif (Documentation Engineer)** added to team roster; leads documentation for CLI/doctor/reload features
- **Leela:** Managed 4-phase architecture and 28 work items across 4 agent-sprints
- **Bender:** Core library updates (IHealthCheckup, BotNexus.Diagnostics)
- **Fry:** Gateway host service integration (ConfigReloadService, REST endpoints)
- **Ralph:** 443-test validation suite, first-run scenario test
- **Hermes:** Config models and BotNexusHome edge cases
- **Zapp:** Orchestration of parallel build/test cycles

---

## Next Sprint Forecast

- **Planned:** Agent execution via cron jobs (Phase 2 of Cron Orchestration)
- **Dependencies:** IAgentRunnerFactory (priority), Activity stream integration
- **Risk:** Concurrent agent job throttling (rate-limit design needed)
