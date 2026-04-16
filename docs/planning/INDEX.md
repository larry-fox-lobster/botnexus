# 🗺️ Planning Index

> Maintained by Nova ✨ | Last updated: 2026-07-16
>
> Generated from spec frontmatter via `build-index.ps1` — don't hand-edit, rebuild instead.

---

## 🔥 Active

### 🐛 Bugs

| Item                                                                             | Pri    | Status  | Since   | What's Wrong                                                                 |
|----------------------------------------------------------------------------------|--------|---------|---------|------------------------------------------------------------------------------|
| [bug-session-switching-ui](bug-session-switching-ui/design-spec.md)              | 🔴 high | partial | Apr '26 | Session switching broken during active agent work                            |
| [bug-steering-message-visibility](bug-steering-message-visibility/design-spec.md)| 🟡 med  | draft   | Apr '26 | Steering messages not visible in conversation flow                           |
| [message-queue-injection-timing](message-queue-injection-timing/design-spec.md)  | 🟡 med  | planning| Jul '25 | User messages queued during multi-tool turns not visible until turn completes |

### ✨ Features

| Item                                                                                             | Pri    | Status  | Since   | What It Does                                                               |
|--------------------------------------------------------------------------------------------------|--------|---------|---------|----------------------------------------------------------------------------|
| [feature-ask-user-tool](feature-ask-user-tool/design-spec.md)                                   | 🔴 high | design  | Apr '26 | Interactive ask_user tool — free-form, single/multi choice, hybrid input   |
| [feature-api-documentation](feature-api-documentation/design-spec.md)                           | 🟡 med  | draft   | Jul '25 | REST, SignalR, and .NET API reference — OpenAPI, DocFX, interactive docs   |
| [feature-architecture-documentation](feature-architecture-documentation/design-spec.md)         | 🟡 med  | draft   | Jul '25 | Architecture documentation — arc42, C4, ADRs                               |
| [feature-code-documentation](feature-code-documentation/design-spec.md)                         | 🟡 med  | draft   | Jul '25 | Contributing guide, XML comment standards, developer guides                |
| [feature-context-visibility](feature-context-visibility/design-spec.md)                         | 🟡 med  | draft   | Apr '26 | /context command to show token usage, context window state                 |
| [feature-prompt-templates](feature-prompt-templates/design-spec.md)                             | 🟡 med  | planning| Jul '26 | Saved parameterized prompt templates for agents, cron, and interactive use |
| [feature-spec-driven-squad-automation](feature-spec-driven-squad-automation/design-spec.md)     | 🟡 med  | draft   | Jul '25 | Automate Squad execution based on spec status transitions                  |

### 🔧 Improvements

| Item                                                                                       | Pri    | Status | Since   | What It Improves                                                            |
|--------------------------------------------------------------------------------------------|--------|--------|---------|-----------------------------------------------------------------------------|
| [improvement-memory-lifecycle](improvement-memory-lifecycle/design-spec.md)                | 🔴 high | draft  | Apr '26 | Memory persistence, compaction, and dreaming consolidation                  |
| [improvement-skills-path-resolution](improvement-skills-path-resolution/design-spec.md)   | 🟡 med  | draft  | Apr '26 | Expose skill base path on load so agents can resolve relative file refs     |

### 📋 Process

| Item                                                                       | Status | Since   | Purpose                                                    |
|----------------------------------------------------------------------------|--------|---------|------------------------------------------------------------|
| [feature-planning-pipeline](feature-planning-pipeline/design-spec.md)     | active | Apr '26 | Planning folder conventions and spec lifecycle (this process) |

---

## ✅ Archived / Done

<details>
<summary>🐛 Bugs — 9 resolved</summary>

| Item                                  | Pri      | Status      | Since   | Summary                                                                     |
|---------------------------------------|----------|-------------|---------|-----------------------------------------------------------------------------|
| bug-session-resumption                | 🔥 crit  | in-progress | Apr '26 | Session rehydration fails after gateway restart; regressed                  |
| bug-subagent-spawn-path               | 🔥 crit  | delivered   | Apr '26 | Sub-agent AgentId contains :: creating illegal Windows paths                |
| bug-edit-tool-double-parse            | 🔴 high  | done        | Apr '26 | EditTool double-parses edits argument                                       |
| bug-exec-process-disconnect           | 🔴 high  | done        | Jul '26 | ExecTool and ProcessTool built on wrong assumptions about process lifecycle |
| bug-pathutils-ignores-file-access-policy | 🔴 high | done       | Apr '26 | PathUtils enforces workspace-only, ignoring FileAccessPolicy                |
| bug-session-lifecycle-fragmentation   | 🔴 high  | done        | Apr '26 | 7 session creation paths, no single truth                                   |
| bug-steering-delivery-latency         | 🔴 high  | done        | Apr '26 | Steering messages delivered too late to influence agent behavior             |
| bug-tool-argument-type-mismatch       | 🔴 high  | done        | Apr '26 | Type mismatch between StreamingJsonParser and tool argument parsers          |
| bug-subagent-realtime-updates         | 🟡 med   | done        | Jul '26 | Sub-agent SignalR bridge missing — WebUI only showed on refresh             |

</details>

<details>
<summary>✨ Features — 8 shipped</summary>

| Item                                  | Pri     | Status      | Since   | Summary                                                          |
|---------------------------------------|---------|-------------|---------|------------------------------------------------------------------|
| feature-user-documentation            | 🔴 high | delivered   | Jul '25 | User docs — Diátaxis, tutorials, how-tos, reference, GitHub Pages|
| feature-tool-permission-model         | 🔴 high | done        | Apr '26 | Per-agent file system permission model                           |
| feature-agent-file-access-policy      | 🔴 high | delivered   | Jul '26 | Per-agent file access policy configuration                       |
| feature-subagent-spawning             | 🔴 high | done        | Apr '26 | Sub-agent spawning for parallel work delegation                  |
| feature-session-visibility            | 🔴 high | implemented | Apr '26 | Session visibility rules for multi-session UI                    |
| feature-media-pipeline                | 🔴 high | delivered   | Jul '26 | Audio transcription and extensible media type pipeline           |
| feature-subagent-ui-visibility        | 🟡 med  | delivered   | Apr '26 | Sub-agent sessions visible in WebUI sidebar                      |
| feature-extension-contributed-commands| 🟡 med  | delivered   | Apr '26 | Extension-contributed commands for WebUI/TUI command palette     |

</details>

<details>
<summary>✨ Features — archived drafts</summary>

| Item                        | Pri    | Status | Since   | Summary                                             |
|-----------------------------|--------|--------|---------|-----------------------------------------------------|
| feature-file-watcher-tool   | 🟡 med | draft  | Jul '26 | File watcher tool for reactive file change monitoring|
| feature-infinite-scrollback | 🟡 med | draft  | Jul '25 | Infinite scrollback without DOM wipe; paginated history |
| feature-agent-delay-tool    | 🟡 med | draft  | Jul '26 | Agent delay/wait tool for pausing mid-turn          |
| feature-location-management | 🟡 med | done   | Jul '26 | Location management                                 |

</details>

<details>
<summary>🔧 Improvements — 6 completed</summary>

| Item                                    | Pri    | Status    | Since   | Summary                                                                   |
|-----------------------------------------|--------|-----------|---------|---------------------------------------------------------------------------|
| improvement-heartbeat-service           | 🔴 high | delivered | Jul '26 | Heartbeat service — reliable periodic agent polling via cron              |
| improvement-subagent-completion-handling | 🔴 high | delivered | Jul '26 | Sub-agent completion wake-up — parent agents miss completions when idle   |
| ddd-refactoring                         | 🔴 high | done      | Jul '25 | Domain-driven design refactoring of entire codebase                       |
| improvement-memory-indexing             | 🔴 high | done      | Jul '26 | MemoryIndexer never registered; added hosted service + CLI backfill       |
| improvement-extension-config-inheritance| 🟡 med  | delivered | Jul '25 | World-level extension config defaults with agent-level deep-merge overrides|
| improvement-datetime-awareness          | 🟡 med  | draft     | Apr '26 | Agent datetime/timezone awareness in system prompt                        |
| improvement-agent-trust-paths           | 🟡 med  | draft     | Apr '26 | Configurable per-agent trusted file paths                                 |

</details>

---

> **Legend:** 🔥 critical · 🔴 high · 🟡 medium · 🟢 low · ⚪ unset
>
> **Statuses:** planning → draft → design → ready → in-progress → delivered → done
>
> **Rebuild:** `pwsh docs/planning/build-index.ps1` → JSON → Nova regenerates this file
