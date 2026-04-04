---
updated_at: 2026-04-05T00:00:00Z
focus_area: pi-mono Agent Port
active_issues:
  - leela-agent-port-plan
---

# What We're Focused On

Porting the pi-mono `@mariozechner/pi-agent-core` TypeScript agent package into a new standalone C#/.NET project: **`BotNexus.AgentCore`**. 4-sprint plan covering foundation types, agent loop engine, stateful Agent wrapper, and tests+docs. The new project references only `BotNexus.Providers.Base` — complete isolation from the existing `BotNexus.Agent`.

## Current Sprint
Sprint 1 (Foundation) — waiting for Farnsworth to scaffold the project and define all types/interfaces. Leela gate review required before Sprint 2.

## Team
Leela (Lead), Farnsworth (Platform Dev), Bender (Runtime Dev), Fry (Web Dev), Amy (UI Designer), Hermes (Tester), Kif (Documentation) — cast from Futurama.
