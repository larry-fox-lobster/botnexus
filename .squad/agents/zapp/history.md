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
