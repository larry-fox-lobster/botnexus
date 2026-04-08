# Cron Infrastructure — Architecture Proposal

**Author:** Leela (Lead / Architect)
**Requested by:** Jon Bullen
**Date:** 2026-04-10
**Status:** Proposed
**Reference:** OpenClaw `src/cron/` (TypeScript implementation)

---

## 1. OpenClaw Reference Summary

### How OpenClaw Implements Cron

OpenClaw's cron system is a mature, production-grade subsystem spread across ~25 files under `src/cron/`. Key architecture:

| Component | File(s) | Purpose |
|-----------|---------|---------|
| `CronService` | `service.ts` + `service/*.ts` | Orchestrator — start/stop/list/add/update/remove/run |
| `CronJob` | `types.ts`, `types-shared.ts` | Job model with schedule, payload, delivery, state |
| Timer loop | `service/timer.ts` | `setInterval` (10–60s) evaluating due jobs |
| Store | `service/store.ts` | JSON file persistence (`~/.openclaw/cron/jobs.json`) |
| Isolated agent | `isolated-agent.ts`, `isolated-agent/run.ts` | Ephemeral agent sessions for cron-triggered turns |
| Cron tool | `agents/tools/cron-tool.ts` | Agent-facing tool (status/list/add/update/remove/run/runs/wake) |
| Gateway RPC | `server-methods/cron.ts` | RPC handlers for CLI/WebUI |
| Delivery | `delivery.ts`, `delivery-plan.ts` | Output routing (announce, webhook) |
| Run log | `run-log.ts` | JSONL execution history per job |
| Session reaper | `session-reaper.ts` | Prunes old isolated cron sessions |
| Schedule engine | `schedule.ts` | Uses [Croner](https://github.com/hexagon/croner) for cron expression evaluation |

**OpenClaw's schedule types:**
- `"cron"` — Standard cron expressions with timezone support
- `"every"` — Fixed interval (milliseconds) with optional anchor
- `"at"` — One-shot ISO-8601 timestamp

**OpenClaw's payload types:**
- `"agentTurn"` — Dedicated agent execution in an isolated session (model override, tool allowlist, timeout)
- `"systemEvent"` — Injects text into the main heartbeat session

**OpenClaw's session targets:**
- `"main"` — Heartbeat session (systemEvent only)
- `"isolated"` — Ephemeral `cron:<jobId>` session
- `"current"` — Bound to creating session
- `"session:<name>"` — Persistent named session

**OpenClaw's delivery modes:**
- `"none"` — No output delivery
- `"announce"` — Send result to a channel (Slack, Telegram, etc.)
- `"webhook"` — POST result to a URL

**Key design choices in OpenClaw:**
- JSON file storage (not a database) — simple but limited querying
- Single-process concurrency control via `locked()` wrapper
- Jobs carry full runtime state inline (nextRunAtMs, consecutiveErrors, etc.)
- Startup catch-up: runs missed jobs with stagger to avoid thundering herd
- Failure alerts with cooldown to prevent alert fatigue

### What to Adopt vs. Adapt

| OpenClaw Pattern | BotNexus Approach |
|------------------|-------------------|
| CronService orchestrator | **Adopt** — `CronScheduler` as `BackgroundService` |
| JSON file store | **Adapt** — Use SQLite (matches our memory store pattern) |
| Cron expressions via Croner | **Adapt** — Use [Cronos](https://github.com/HangfireIO/Cronos) (.NET, MIT licensed) |
| Payload types (agentTurn, systemEvent) | **Adapt** — Polymorphic `ICronAction` with DI (more extensible) |
| Session targets (main, isolated, current, named) | **Adopt** — Map to our `ChannelType = "cron"` with session targets |
| Agent cron tool | **Adopt** — `CronTool : IAgentTool` with same action verbs |
| Delivery system | **Defer** — Out of scope for v1 (agents can use their own channel tools) |
| Failure alerts | **Defer** — v1 logs failures; alert system comes later |
| Run log (JSONL) | **Adapt** — Store run history in SQLite `cron_runs` table |
| Session reaper | **Adapt** — Extend existing `SessionCleanupService` |
| Gateway RPC handlers | **Adapt** — REST API controllers (matches our pattern) |

---

## 2. Architecture

### Core Interfaces

```csharp
// src/cron/BotNexus.Cron/ICronAction.cs
public interface ICronAction
{
    /// <summary>Unique action type key (e.g., "agent-prompt", "webhook", "shell").</summary>
    string ActionType { get; }

    /// <summary>Execute the cron action.</summary>
    Task ExecuteAsync(CronExecutionContext context, CancellationToken cancellationToken);
}
```

```csharp
// src/cron/BotNexus.Cron/CronExecutionContext.cs
public sealed record CronExecutionContext
{
    /// <summary>The job being executed.</summary>
    public required CronJob Job { get; init; }

    /// <summary>When the execution was triggered.</summary>
    public required DateTimeOffset TriggeredAt { get; init; }

    /// <summary>Whether this is a manual (run-now) vs scheduled trigger.</summary>
    public required CronTriggerType TriggerType { get; init; }

    /// <summary>Scoped service provider for resolving dependencies.</summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>Logger scoped to this execution.</summary>
    public required ILogger Logger { get; init; }
}

public enum CronTriggerType { Scheduled, Manual }
```

### Built-in Action Types

**`AgentPromptAction`** — Sends a message to an agent, creating an isolated cron session:

```csharp
// src/cron/BotNexus.Cron/Actions/AgentPromptAction.cs
public sealed class AgentPromptAction(
    ISessionStore sessionStore,
    IAgentSupervisor agentSupervisor,
    IAgentCommunicator agentCommunicator) : ICronAction
{
    public string ActionType => "agent-prompt";

    public async Task ExecuteAsync(CronExecutionContext context, CancellationToken ct)
    {
        var job = context.Job;
        var sessionId = $"cron:{job.Id}:{context.TriggeredAt:yyyyMMdd-HHmmss}";

        // Create cron session (appears in WebUI)
        var session = await sessionStore.GetOrCreateAsync(sessionId, job.AgentId, ct);
        session.ChannelType = "cron";
        session.Metadata["cronJobId"] = job.Id;
        session.Metadata["cronJobName"] = job.Name;
        session.Metadata["triggerType"] = context.TriggerType.ToString();

        // Dispatch prompt to agent
        await agentCommunicator.SendAsync(job.AgentId, sessionId, job.ActionConfig.Message, ct);
        await sessionStore.SaveAsync(session, ct);
    }
}
```

**`WebhookAction`** — POSTs to a URL with job context:

```csharp
// src/cron/BotNexus.Cron/Actions/WebhookAction.cs
public sealed class WebhookAction(IHttpClientFactory httpClientFactory) : ICronAction
{
    public string ActionType => "webhook";

    public async Task ExecuteAsync(CronExecutionContext context, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("CronWebhook");
        var payload = new { jobId = context.Job.Id, jobName = context.Job.Name,
                            triggeredAt = context.TriggeredAt, config = context.Job.ActionConfig };
        await client.PostAsJsonAsync(context.Job.ActionConfig.WebhookUrl, payload, ct);
    }
}
```

**`ShellAction`** (future) — Executes a shell command. Deferred to v2 due to security surface.

### CronScheduler (Hosted Service)

```csharp
// src/cron/BotNexus.Cron/CronScheduler.cs
public sealed class CronScheduler(
    ICronStore cronStore,
    IEnumerable<ICronAction> actions,
    IServiceScopeFactory scopeFactory,
    IOptions<CronOptions> options,
    ILogger<CronScheduler> logger) : BackgroundService
{
    // Timer-based loop (configurable tick interval, default 30s)
    // On each tick:
    //   1. Load enabled jobs from CronStore
    //   2. Evaluate schedule — is job due? (using Cronos)
    //   3. Dispatch due jobs to matching ICronAction
    //   4. Record run result to CronStore
    //   5. Compute and persist next run time
    //
    // Concurrency: maxConcurrentRuns (default 1) via SemaphoreSlim
    // Startup catch-up: runs missed jobs with configurable stagger
    // Error handling: catch per-job, log, increment consecutiveErrors
}
```

### CronStore (SQLite)

```csharp
// src/cron/BotNexus.Cron/CronStore.cs
public sealed class CronStore : ICronStore
{
    // Location: ~/.botnexus/data/cron.sqlite
    // Schema:
    //
    // cron_jobs:
    //   id TEXT PRIMARY KEY,
    //   name TEXT NOT NULL,
    //   description TEXT,
    //   agent_id TEXT NOT NULL,
    //   schedule_expr TEXT NOT NULL,       -- cron expression
    //   schedule_timezone TEXT,            -- IANA timezone
    //   action_type TEXT NOT NULL,         -- "agent-prompt", "webhook"
    //   action_config_json TEXT NOT NULL,  -- polymorphic config
    //   enabled INTEGER NOT NULL DEFAULT 1,
    //   created_by TEXT,                   -- agent or user who created
    //   created_at TEXT NOT NULL,
    //   updated_at TEXT NOT NULL,
    //   next_run_at TEXT,
    //   last_run_at TEXT,
    //   last_run_status TEXT,              -- "ok", "error", "timeout"
    //   last_error TEXT,
    //   consecutive_errors INTEGER NOT NULL DEFAULT 0,
    //   max_retries INTEGER NOT NULL DEFAULT 3
    //
    // cron_runs:
    //   id INTEGER PRIMARY KEY AUTOINCREMENT,
    //   job_id TEXT NOT NULL REFERENCES cron_jobs(id),
    //   started_at TEXT NOT NULL,
    //   completed_at TEXT,
    //   status TEXT NOT NULL,              -- "running", "ok", "error", "timeout"
    //   trigger_type TEXT NOT NULL,        -- "scheduled", "manual"
    //   session_id TEXT,                   -- linked session (if created)
    //   error_message TEXT,
    //   duration_ms INTEGER
}
```

### Session Integration

Cron sessions integrate with the existing session system naturally:

1. **`AgentPromptAction`** creates sessions via `ISessionStore.GetOrCreateAsync()` with `ChannelType = "cron"`
2. Sessions are stored in the same session store as SignalR/Telegram/TUI sessions
3. `SessionsController.ListAsync()` already returns all sessions — cron sessions appear alongside others
4. `SessionCleanupService` already handles expiry — cron sessions respect the same TTL

**No changes needed to existing session infrastructure.** The `ChannelType = "cron"` field is the only distinguishing marker.

---

## 3. Config Schema

Cron jobs are defined in the platform config (`~/.botnexus/config.json`), in addition to being created dynamically via API/tool:

```jsonc
{
  "cron": {
    "enabled": true,
    "tickIntervalSeconds": 30,
    "maxConcurrentRuns": 2,
    "sessionRetentionHours": 24,
    "jobs": {
      "daily-summary": {
        "schedule": "0 9 * * *",
        "timezone": "America/Los_Angeles",
        "action": "agent-prompt",
        "agentId": "nova",
        "message": "Give me a summary of yesterday's activity across all agents.",
        "enabled": true
      },
      "hourly-health": {
        "schedule": "0 * * * *",
        "action": "webhook",
        "webhookUrl": "https://hooks.example.com/health",
        "enabled": true
      },
      "weekly-cleanup": {
        "schedule": "0 2 * * 0",
        "action": "agent-prompt",
        "agentId": "hermes",
        "message": "Archive sessions older than 7 days and compact memory stores.",
        "enabled": false
      }
    }
  }
}
```

**Options class:**

```csharp
public sealed class CronOptions
{
    public const string SectionName = "cron";

    public bool Enabled { get; set; } = true;
    public int TickIntervalSeconds { get; set; } = 30;
    public int MaxConcurrentRuns { get; set; } = 2;
    public int SessionRetentionHours { get; set; } = 24;
    public int MaxRetries { get; set; } = 3;
    public Dictionary<string, CronJobConfig> Jobs { get; set; } = [];
}

public sealed class CronJobConfig
{
    public required string Schedule { get; set; }
    public string? Timezone { get; set; }
    public required string Action { get; set; }
    public string? AgentId { get; set; }
    public string? Message { get; set; }
    public string? WebhookUrl { get; set; }
    public bool Enabled { get; set; } = true;
}
```

Config-defined jobs are seeded into the `CronStore` on startup if not already present (idempotent upsert by name).

---

## 4. Agent Cron Tool

The `CronTool` follows the `IAgentTool` contract and provides agents with full cron CRUD:

```csharp
// src/cron/BotNexus.Cron/Tools/CronTool.cs
public sealed class CronTool(ICronStore cronStore, CronScheduler scheduler) : IAgentTool
{
    public string Name => "cron";
    public string Label => "Cron Job Manager";
}
```

### Tool Actions

| Action | Description | Parameters |
|--------|-------------|------------|
| `list` | List cron jobs | `agentId?` (filter), `includeDisabled?` |
| `create` | Create a new cron job | `name`, `schedule`, `action`, `agentId`, `message?`, `webhookUrl?`, `enabled?` |
| `update` | Modify an existing job | `id`, plus any field to patch |
| `delete` | Remove a cron job | `id` |
| `run-now` | Trigger immediate execution | `id` |
| `status` | Get scheduler status | _(none)_ |

### Permission Model

```csharp
// Permission check in CronTool.ExecuteAsync:
//
// 1. Resolve the calling agent's ID from tool execution context
// 2. For create/update/delete: agent can only manage jobs where
//    job.agentId == callingAgentId
//    UNLESS the agent has "allowCrossAgentCron: true" in its config
// 3. For list: agents see all jobs (read-only visibility)
// 4. For run-now: same ownership rules as update
```

The permission flag is set in the agent descriptor's metadata:

```jsonc
{
  "agents": {
    "nova": {
      "metadata": {
        "allowCrossAgentCron": true  // Nova can manage other agents' cron jobs
      }
    }
  }
}
```

### Tool Schema (JSON Schema for LLM)

```json
{
  "name": "cron",
  "description": "Manage scheduled recurring jobs. Actions: list, create, update, delete, run-now, status.",
  "parameters": {
    "type": "object",
    "properties": {
      "action": {
        "type": "string",
        "enum": ["list", "create", "update", "delete", "run-now", "status"],
        "description": "The cron operation to perform."
      },
      "id": { "type": "string", "description": "Job ID (required for update/delete/run-now)." },
      "name": { "type": "string", "description": "Job name (required for create)." },
      "schedule": { "type": "string", "description": "Cron expression, e.g., '0 9 * * *' (required for create)." },
      "timezone": { "type": "string", "description": "IANA timezone for the schedule." },
      "actionType": { "type": "string", "enum": ["agent-prompt", "webhook"], "description": "Action type." },
      "agentId": { "type": "string", "description": "Target agent for agent-prompt actions." },
      "message": { "type": "string", "description": "Prompt message for agent-prompt actions." },
      "webhookUrl": { "type": "string", "description": "URL for webhook actions." },
      "enabled": { "type": "boolean", "description": "Whether the job is enabled." },
      "includeDisabled": { "type": "boolean", "description": "Include disabled jobs in list." }
    },
    "required": ["action"]
  }
}
```

---

## 5. Session Integration

### Session Creation Flow

```
CronScheduler tick
  └─ Job is due
     └─ Resolve ICronAction by ActionType
        └─ AgentPromptAction.ExecuteAsync()
           ├─ Create session: "cron:{jobId}:{timestamp}"
           │   ChannelType = "cron"
           │   Metadata = { cronJobId, cronJobName, triggerType }
           ├─ IAgentCommunicator.SendAsync(agentId, sessionId, message)
           └─ ISessionStore.SaveAsync(session)
```

### WebUI Visibility

Cron sessions appear in the sessions list alongside SignalR sessions:

- **List view:** `GET /api/sessions` returns all sessions. WebUI can filter by `channelType`:
  - All sessions (default)
  - `signalr` — Interactive WebSocket sessions
  - `cron` — Scheduled job sessions
  - `telegram` — Telegram channel sessions
- **Session detail:** Clicking a cron session shows the full conversation (prompt + agent response + tool calls)
- **Visual indicator:** WebUI can use the `channelType` field to show a clock icon or "Scheduled" badge

### Session Resumption

Users can resume cron-created sessions to interact with the output:

1. User clicks on a cron session in WebUI
2. WebUI calls `POST /hub/gateway/JoinSession` with the cron session ID
3. Gateway's `GatewayHost` creates/resumes the agent instance for that session
4. User can now send follow-up messages via SignalR (the session effectively becomes a hybrid cron+signalr session)
5. The session's `ChannelType` remains `"cron"` to preserve origin tracking

**No new infrastructure required.** The existing `JoinSession` → `DispatchAsync` pipeline already supports resuming any session regardless of origin channel. The only WebUI change is surfacing cron sessions in the session list and allowing click-to-resume.

---

## 6. API Endpoints

New controller: `CronController` following existing REST patterns:

```csharp
[ApiController]
[Route("api/cron")]
public sealed class CronController(ICronStore cronStore, CronScheduler scheduler)
```

| Method | Route | Description | Response |
|--------|-------|-------------|----------|
| `GET` | `/api/cron` | List all cron jobs | `CronJobSummary[]` |
| `GET` | `/api/cron/{id}` | Get job details + recent runs | `CronJobDetail` |
| `POST` | `/api/cron` | Create a new job | `CronJobSummary` (201) |
| `PUT` | `/api/cron/{id}` | Update a job | `CronJobSummary` |
| `DELETE` | `/api/cron/{id}` | Delete a job | 204 No Content |
| `POST` | `/api/cron/{id}/run` | Trigger immediate execution | `CronRunResult` (202) |
| `GET` | `/api/cron/{id}/runs` | Get execution history | `CronRunEntry[]` |
| `GET` | `/api/cron/status` | Scheduler status (enabled, job counts, next run) | `CronSchedulerStatus` |

**Query parameters:**
- `GET /api/cron?agentId=nova` — Filter jobs by agent
- `GET /api/cron?enabled=true` — Filter by enabled state
- `GET /api/cron/{id}/runs?limit=20&offset=0` — Paginated run history

---

## 7. Implementation Plan

### Wave 1 — Core Infrastructure (Farnsworth)

**Scope:** Project scaffold, models, store, scheduler skeleton

- Create `BotNexus.Cron` project with solution reference
- `CronJob` model, `CronOptions`, `CronJobConfig`
- `ICronStore` interface + `SqliteCronStore` implementation (schema, CRUD, WAL mode)
- `ICronAction` interface + `CronExecutionContext`
- `CronScheduler` as `BackgroundService` (timer loop, schedule evaluation via Cronos, concurrency control)
- Unit tests: store CRUD, schedule evaluation, timer logic
- `CronServiceCollectionExtensions.AddBotNexusCron()` for DI registration

**Depends on:** Nothing (greenfield)
**Tests:** ~20 unit tests

### Wave 2 — Actions + Agent Integration (Bender)

**Scope:** Action types, session creation, agent dispatch

- `AgentPromptAction` — creates cron session, dispatches to agent
- `WebhookAction` — HTTP POST with job context
- Cron session creation with `ChannelType = "cron"` and metadata
- Wire actions into `CronScheduler` dispatch pipeline
- Integration tests: action execution, session creation, agent response
- Extend `SessionCleanupService` to respect `sessionRetentionHours` for cron sessions

**Depends on:** Wave 1
**Tests:** ~15 integration tests

### Wave 3 — API + Agent Tool (Hermes)

**Scope:** REST endpoints, agent-facing tool

- `CronController` — full REST API (list, get, create, update, delete, run, runs, status)
- `CronTool : IAgentTool` — agent CRUD with permission model
- Register `CronTool` in tool registry
- Config seeding — load `cron.jobs` from config into store on startup
- API tests + tool tests

**Depends on:** Wave 1 (store), partially Wave 2 (actions for run-now)
**Tests:** ~15 tests (API + tool)

### Wave 4 — WebUI + Polish (Amy / Hermes)

**Scope:** Frontend integration, session resumption, observability

- WebUI: cron session list filtering (channel type badge/icon)
- WebUI: click-to-resume cron sessions (no backend changes needed)
- WebUI: cron job management page (list, create, enable/disable) — optional, API exists
- Structured logging for all cron operations
- Run history pruning (max rows per job, configurable)
- End-to-end integration tests

**Depends on:** Waves 1–3
**Tests:** ~10 E2E tests

### Estimated Total

| | Wave 1 | Wave 2 | Wave 3 | Wave 4 |
|---|--------|--------|--------|--------|
| **Owner** | Farnsworth | Bender | Hermes | Amy / Hermes |
| **Focus** | Core | Actions | API + Tool | UI + Polish |
| **Tests** | ~20 | ~15 | ~15 | ~10 |

**Total:** 4 waves, ~60 new tests, 1 new project (`BotNexus.Cron`)

---

## 8. Project Structure

```
src/cron/BotNexus.Cron/
├── BotNexus.Cron.csproj
├── ICronAction.cs                          # Action interface
├── CronExecutionContext.cs                 # Execution context record
├── CronJob.cs                              # Job model
├── CronOptions.cs                          # Configuration options
├── CronScheduler.cs                        # BackgroundService — timer + dispatch
├── ICronStore.cs                           # Store interface
├── Actions/
│   ├── AgentPromptAction.cs                # Creates cron session, prompts agent
│   └── WebhookAction.cs                    # HTTP POST to webhook URL
├── Store/
│   └── SqliteCronStore.cs                  # SQLite persistence (WAL mode)
├── Tools/
│   └── CronTool.cs                         # IAgentTool — agent CRUD
└── Extensions/
    └── CronServiceCollectionExtensions.cs  # DI registration
```

**Test project:**
```
tests/BotNexus.Cron.Tests/
├── BotNexus.Cron.Tests.csproj
├── CronSchedulerTests.cs
├── SqliteCronStoreTests.cs
├── AgentPromptActionTests.cs
├── WebhookActionTests.cs
├── CronToolTests.cs
└── CronControllerTests.cs
```

**API controller** lives in existing `BotNexus.Gateway.Api`:
```
src/gateway/BotNexus.Gateway.Api/Controllers/CronController.cs
```

---

## Design Decisions

### D1: SQLite over JSON file storage

**OpenClaw uses JSON files.** We use SQLite because:
- Matches our existing storage pattern (`SqliteMemoryStore`)
- Enables efficient querying (filter by agent, status, date ranges)
- Run history as a proper table with pagination (vs. JSONL log files)
- WAL mode for concurrent reads during scheduler ticks
- Atomic writes without manual file locking

### D2: Polymorphic `ICronAction` with DI over hardcoded payload types

**OpenClaw uses discriminated unions** (`kind: "agentTurn" | "systemEvent"`). We use interface + DI because:
- C# doesn't have discriminated unions (yet)
- New action types can be added via DI registration without modifying core
- Extension assemblies can contribute custom cron actions
- Follows BotNexus's existing extensibility pattern (tools, providers, channels)

### D3: Cronos library over NCrontab or Quartz

- **Cronos:** Lightweight, MIT license, supports 5-field and 6-field (with seconds), timezone-aware, `CronExpression.Parse()` → `GetNextOccurrence()`. No scheduling runtime — just expression evaluation.
- **NCrontab:** Simpler but no timezone support.
- **Quartz.NET:** Full scheduler — massively over-engineered for our needs (we have our own `BackgroundService` loop).

Cronos fits perfectly: we own the timer loop, we just need expression evaluation.

### D4: Cron sessions as first-class sessions (not separate storage)

Rather than a separate "cron result" store, cron executions create real `GatewaySession` entries. This means:
- Zero changes to session list/detail/history APIs
- Cron conversations are immediately visible in WebUI
- Users can resume and interact with cron results
- Session cleanup applies uniformly

### D5: Config seeding + dynamic creation (dual-path)

Jobs can come from two sources:
1. **Config file** (`cron.jobs` section) — seeded into SQLite on startup, idempotent by name
2. **Dynamic** — created via API or agent tool at runtime

Config-seeded jobs are marked with `createdBy = "config"` and are re-applied on restart (config wins for fields it specifies). Dynamically created jobs persist in SQLite across restarts.

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Missed jobs after restart | Medium | Startup catch-up: evaluate all enabled jobs on boot, run any past-due |
| Thundering herd (many jobs at same time) | Medium | `maxConcurrentRuns` semaphore + stagger for same-second schedules |
| Agent not available when cron fires | Low | Log error, increment `consecutiveErrors`, retry on next tick |
| Unbounded run history | Low | Prune `cron_runs` table (max rows per job, configurable, default 100) |
| Permission bypass via tool | Low | Ownership check in `CronTool.ExecuteAsync` before any mutation |
| Long-running cron action blocks scheduler | Medium | Per-action timeout (configurable, default 5 min) via `CancellationTokenSource` |

---

## NuGet Dependencies

| Package | Purpose | License |
|---------|---------|---------|
| `Cronos` | Cron expression parsing + next occurrence | MIT |
| `Microsoft.Data.Sqlite` | SQLite access (already used by memory store) | MIT |

No new external dependencies beyond `Cronos`. `Microsoft.Data.Sqlite` is already in the dependency graph.
