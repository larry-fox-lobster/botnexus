# Project Context

- **Owner:** Jon Bullen
- **Project:** BotNexus — modular AI agent execution platform (OpenClaw-like) built in C#/.NET. Lean core with extension points for assembly-based plugins. Multiple agent execution modes (local, sandbox, container, remote). Currently focusing on local execution. SOLID patterns with vigilance against over-abstraction. Comprehensive testing (unit + E2E integration).
- **Stack:** C# (.NET latest), modular class libraries: Core, Agent, Api, Channels (Base/Discord/Slack/Telegram), Command, Cron, Gateway, Heartbeat, Providers (Base/Anthropic/OpenAI/Copilot), Session, Tools.GitHub, WebUI
- **Created:** 2026-04-01

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- 2026-04-01: Added to team as Consistency Reviewer after Jon caught docs/architecture.md not reflecting the ~/.botnexus/ unified config that was implemented after the docs were written. Multi-agent workflows have a systemic consistency gap — each agent updates their own files but nobody checks the seams.

## 2026-04-02 — Team Updates

- **Nibbler Onboarding:** New team member added. Role: Consistency Reviewer. Owns post-sprint consistency audits (docs vs code, docs vs docs, code comments vs behavior, README vs current state).
- **New Ceremony:** Consistency Review ceremony established. Trigger: after sprint completion or architectural changes. Owner: Nibbler. First implementation: Leela's audit (2026-04-02) found 22 issues across 5 files.
- **Related Decision:** "Cross-Document Consistency Checks as a Team Ceremony" merged into decisions.md (2026-04-01T18:54Z directive from Jon).
