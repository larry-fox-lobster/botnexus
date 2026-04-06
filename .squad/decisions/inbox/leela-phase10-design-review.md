# Phase 10 Design Review — Grade A-

**Reviewer:** Leela  
**Date:** 2026-04-06  
**Scope:** 6 commits across 3 agents (Farnsworth ×4, Bender ×1, Hermes ×1)

## Decisions

1. **WebSocket handler decomposition approved** — `GatewayWebSocketHandler` → orchestrator (150 lines), `WebSocketConnectionManager` (166 lines), `WebSocketMessageDispatcher` (296 lines). Clean SRP split with preserved endpoint contracts.

2. **PUT AgentId validation approved** — Returns 400 on route/body mismatch, falls back to route value on empty body. Phase 9 P1 resolved.

3. **CORS verb restriction approved** — Production restricts to `GET, POST, PUT, DELETE, OPTIONS`. Development keeps permissive CORS. Phase 9 P1 resolved.

4. **CLI architecture needs Phase 11 work** — `Program.cs` is 850+ lines of top-level statements. P1: decompose into command handler classes. P1: add test coverage for config get/set reflection.

5. **Deployment test harness approved** — `WebApplicationFactory<Program>` with isolated `BOTNEXUS_HOME` temp roots. Solid config layering coverage.

## P1 Items for Phase 11

- [ ] Decompose `BotNexus.Cli/Program.cs` into command handler classes
- [ ] Add unit tests for CLI config get/set reflection logic
- [ ] Copilot conformance tests duplicate OpenAI (carried from Phase 9)

## Carried Forward

- StreamAsync task leak (deferred — frozen code)
- SessionHistoryResponse location (Abstractions.Models)
- SequenceAndPersistPayloadAsync double-serialization
