# Squad Decisions

## Active Decisions

### 1. Initial Architecture Review Findings (2026-04-01)

**Author:** Leela (Lead/Architect)  
**Status:** Proposed  
**Requested by:** Jon Bullen

**Context:** First-pass architecture review of BotNexus — the initial port and setup. No PRD, no spec, no docs. System has never been built, deployed, or run. This review establishes the baseline.

**Key Findings:**

**The Good:**
- Clean contract layer: Core defines 13 well-designed interfaces. Dependencies flow inward. No circular references.
- Build is green: Solution compiles on .NET 10.0 with 0 errors, 2 minor warnings. All 124 tests pass.
- SOLID compliance: Interfaces are small and focused. Single implementations justified by extension model.
- Hierarchical config: BotNexusConfig well-structured with per-agent overrides and sensible defaults.
- Test foundation: 121 unit tests + 3 integration tests. xUnit + FluentAssertions + Moq.
- Agent loop: Well-structured agentic loop with tool calling, session persistence, hooks, MCP support.

**The Concerning:**
- Channel registration gap: Discord, Slack, Telegram are implemented but never registered in Gateway DI.
- Anthropic provider incomplete: OpenAI supports tool calling; Anthropic does not. No DI extension method.
- No auth anywhere: No authentication/authorization on Gateway REST, WebSocket, or API endpoints.
- Sync-over-async hazard: `MessageBusExtensions.Publish()` wraps async with `.GetAwaiter().GetResult()` — deadlock timebomb.
- ProviderRegistry unused: Class exists but never registered in DI or referenced. Dead code.
- Slack webhook gap: Slack channel uses webhook mode but Gateway has no incoming webhook endpoint.
- No plugin/assembly loading: README mentions extensibility, but no mechanism exists.
- Gateway dispatches to first runner only: `runners[0].RunAsync()` — only first IAgentRunner is used.

**P0 — Must Fix Before First Run:**
1. Register channel implementations in Gateway DI (conditional on config Enabled flags)
2. Add Anthropic DI extension (matching OpenAI pattern)
3. Remove sync-over-async wrapper (delete or rewrite MessageBusExtensions.Publish())
4. Add basic configuration documentation (appsettings.json structure)

**P1 — Should Fix Soon:**
5. Add authentication (at minimum, API key auth on REST/WebSocket)
6. Implement Anthropic tool calling (feature parity with OpenAI)
7. Fix first-runner-only dispatch (route by agent name or document intentional single-runner design)
8. Add Slack webhook endpoint (Gateway needs POST for Slack events)
9. Fix CA2024 warning in AnthropicProvider streaming

**P2 — Should Plan:**
10. Design plugin architecture (assembly loading, plugin discovery, dynamic registration)
11. Add observability (metrics, tracing, health check endpoints)
12. Documentation (architecture, setup guide, API reference)
13. Evaluate ProviderRegistry (integrate or remove dead code)

**Status:** SUPERSEDED by Rev 2 Implementation Plan (see below). This review identified gaps; Rev 2 provides the roadmap to fix them.

---

### 2. User Directives — Process & Architecture (2026-04-01)

**Collected by:** Jon Bullen via Copilot CLI  
**Status:** Approved  

**2a. Dynamic Assembly Loading for Extensions** (2026-04-01T16:29Z)

**What:** All channels, providers, tools, etc. must NOT be available by default. They should only be dynamically loaded into the DI container when referenced in configuration. Folder-based organization: each area (providers, tools, channels) has a folder with sub-folders per implementation (e.g., providers/copilot, channel/discord, channel/telegram). Configuration refers to folder names and the core platform loads assemblies from those folders that expose the required interfaces. This keeps things abstracted and reduces security risk of things being loaded and exposing endpoints without the user realising.

**Why:** User request — captured for team memory. This is a foundational architectural decision that reshapes how the platform handles extensibility and security.

**2b. Conventional Commits Format** (2026-04-01T16:43Z)

**What:** Always use conventional commit format (e.g., `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`). Commit changes as each area of work is completed — not one big commit at the end. This keeps history clean and makes it easy to see what changed and roll back if needed.

**Why:** User request — captured for team memory. This is a process rule that all agents must follow when committing code.

**2c. Copilot Provider is P0, OAuth Authentication** (2026-04-01T16:46Z)

**What:** When prioritizing provider work, Copilot is always P0 — it is the only provider Jon uses. Authentication to Copilot should be via OAuth, following the same approach used by Nanobot (the project BotNexus was ported from and based on). Other providers (OpenAI, Anthropic) are lower priority.

**Why:** User request — captured for team memory. This shapes provider priority and auth implementation. The team should look at Nanobot's OAuth flow as the reference pattern for Copilot provider auth.

---

### 3. BotNexus Implementation Plan — Rev 2 (2026-04-01)

**Author:** Leela (Lead/Architect)  
**Date:** 2026-04-01 (revised 2026-04-01)  
**Status:** Proposed  
**Requested by:** Jon Bullen  

**Executive Summary:**

Jon's directives fundamentally reshape the roadmap. The original P2 item — "Design plugin architecture" — is now the foundation that everything else builds on. Channels, providers, and tools must be dynamically loaded from folder-based extension assemblies, referenced by configuration. Nothing loads unless explicitly configured.

This plan re-examines all P0/P1/P2 items through the dynamic loading lens, merges what overlaps, reorders by dependency, and maps every work item to a team member.

**Rev 2 Key Changes:**
- Jon's directive makes Copilot provider **P0 — higher priority than all other providers**
- Copilot uses OpenAI-compatible API (base URL: https://api.githubcopilot.com)
- Auth via OAuth device code flow, not API key
- Introduces OAuth abstractions in Core and dedicated BotNexus.Providers.Copilot extension
- Provider priority reordered: **Copilot (P0) > OpenAI (P1) > Anthropic (P2)**
- All work follows conventional commit format (feat/fix/refactor/docs/test/chore)

---

### 4. Decision: GitHub Copilot Provider — P0, OAuth Device Code Flow (2026-04-01)

**Author:** Leela (Lead/Architect)  
**Date:** 2026-04-01  
**Status:** Approved  
**Requested by:** Jon Bullen  

**Context:** Jon's directive: "If you need to preference work on providers, copilot is always P0, it will be all I ever use. This should be via OAuth as per the approach Nanobot used."

BotNexus was ported from Nanobot. In Nanobot, the GitHub Copilot provider is defined as:
```python
ProviderSpec(
    name="github_copilot",
    backend="openai_compat",
    default_api_base="https://api.githubcopilot.com",
    is_oauth=True,
)
```

Key facts:
- Copilot uses the **OpenAI-compatible API format** (same as OpenAI provider)
- Base URL: `https://api.githubcopilot.com`
- Auth: **OAuth device code flow** — no API key, token acquired at runtime
- In Nanobot, `is_oauth=True` providers skip API key validation and handle their own auth

---

## Part 1: Dynamic Assembly Loading Architecture

### 1.1 Folder Structure Convention

```
{AppRoot}/
  extensions/
    channels/
      discord/        → BotNexus.Channels.Discord.dll + dependencies
      telegram/       → BotNexus.Channels.Telegram.dll + dependencies
      slack/          → BotNexus.Channels.Slack.dll + dependencies
    providers/
      copilot/        → BotNexus.Providers.Copilot.dll + dependencies
      openai/         → BotNexus.Providers.OpenAI.dll + dependencies
      anthropic/      → BotNexus.Providers.Anthropic.dll + dependencies
    tools/
      github/         → BotNexus.Tools.GitHub.dll + dependencies
```

Each sub-folder is a self-contained deployment unit. The `extensions/` root path is configurable via `ExtensionsPath` in `BotNexusConfig`. Default: `./extensions`.

### 1.2 Configuration Model

Current config has hard-coded typed properties. These must become **dictionary-based** so the set of extensions is driven entirely by config.

**Key changes:**
- `ProvidersConfig` becomes `Dictionary<string, ProviderConfig>` keyed by folder name
- `ProviderConfig` gains `Auth` discriminator: `"apikey"` (default) or `"oauth"`
- `ChannelsConfig` moves per-channel config to `Instances: Dictionary<string, ChannelConfig>`
- `ToolsConfig` adds `Extensions: Dictionary<string, Dictionary<string, object>>` for dynamic tools
- Root config adds `ExtensionsPath: string`

### 1.3 Discovery and Loading Process

**Core class:** `ExtensionLoader` (in `BotNexus.Core` or new `BotNexus.Extensions` project)

**Loading sequence at startup:**
1. Read config — Enumerate keys under Providers, Channels.Instances, Tools.Extensions
2. Resolve folders — Compute `{ExtensionsPath}/{type}/{key}/` for each key
3. Validate folder — Log warning and skip if missing
4. Load assemblies — Create `AssemblyLoadContext` per extension (collectible for hot-reload)
5. Scan for types — Search loaded assemblies for concrete types implementing target interface
6. Register in DI — `ServiceProvider.AddSingleton<ILlmProvider>(instance)`

---

## Part 2: OAuth Core Abstractions (Phase 1 P0 Item)

New in Core namespace `BotNexus.Core.OAuth`:

### IOAuthProvider Interface
```csharp
Task<string> GetAccessTokenAsync(CancellationToken)
bool HasValidToken
```
Acquires valid token, performing OAuth flow if needed. HasValidToken checks if cached token is still valid.

### IOAuthTokenStore Interface
```csharp
Task<OAuthToken> LoadTokenAsync(string providerName, CancellationToken)
Task SaveTokenAsync(string providerName, OAuthToken token, CancellationToken)
Task ClearTokenAsync(string providerName, CancellationToken)
```
Abstraction for secure token persistence. Default implementation: encrypted file storage at `~/.botnexus/tokens/{providerName}.json`
Future: OS keychain integration (Windows Credential Manager, macOS Keychain, Linux Secret Service)

### OAuthToken Record
```csharp
string AccessToken
DateTime ExpiresAt
string? RefreshToken (optional)
```

### Integration with Extension Loader
ProviderConfig.Auth discriminator: "apikey" or "oauth"
ExtensionLoader checks Auth field. For "oauth", validates IOAuthProvider is implemented, skips API key validation.

---

## Part 3: GitHub Copilot Provider (Phase 2 P0 Item)

**Provider Name:** copilot  
**Base URL:** https://api.githubcopilot.com  
**Auth:** OAuth device code flow

### Implementation

New project: `BotNexus.Providers.Copilot`  
Implements: `ILlmProvider` (via `LlmProviderBase`) + `IOAuthProvider`  
HTTP format: OpenAI-compatible chat completions, streaming, tool calling  
Same request/response DTOs as OpenAI provider

### OAuth Device Code Flow

1. `POST https://github.com/login/device/code` with `client_id`
   Response: `{ device_code, user_code, verification_uri, interval, expires_in }`

2. Display to user: "Go to {verification_uri} and enter code: {user_code}"

3. Poll `POST https://github.com/login/oauth/access_token` with `device_code`
   Every {interval} seconds until token returned, user denies, or timeout

4. Cache token via `IOAuthTokenStore`
5. Use as Bearer token in `Authorization` header
6. On subsequent requests: check `HasValidToken`, re-authenticate if expired

### Shared OpenAI-Compatible Code

Extract shared request/response DTOs, SSE streaming parser, HTTP client patterns to `BotNexus.Providers.Base`
Both OpenAI and Copilot reference Providers.Base and use shared HTTP layer
Each provider adds its own auth mechanism

### Config Shape

```json
{
  "BotNexus": {
    "Providers": {
      "copilot": {
        "Auth": "oauth",
        "DefaultModel": "gpt-4o",
        "ApiBase": "https://api.githubcopilot.com",
        "OAuthClientId": "..."
      }
    }
  }
}
```

---

## Part 4: Implementation Phases & Work Items (24 Items)

### Phase 1: Core Extensions (Foundations) — 7 Items

**P0 (5 items — Blocking all subsequent work):**

| ID | Work Item | Owner | Points | Description |
|---|---|---|---|---|
| 1 | provider-dynamic-loading | Farnsworth | 50 | Core ExtensionLoader class, AssemblyLoadContext per extension, folder discovery, DI registration |
| 2 | channel-di-registration | Amy | 25 | Register Discord, Slack, Telegram in Gateway DI conditional on config |
| 3 | anthropic-provider-di | Amy | 10 | Add AddAnthropicProvider extension method matching OpenAI pattern |
| 4 | oauth-core-abstractions | Farnsworth | 20 | IOAuthProvider, IOAuthTokenStore, OAuthToken in Core.OAuth namespace |
| 5 | provider-openai-sync-fix | Fry | 30 | Remove MessageBusExtensions.Publish sync-over-async (.GetAwaiter().GetResult()), redesign to fully async |

**P1 (2 items — Important, not blocking):**

| ID | Work Item | Owner | Points | Description |
|---|---|---|---|---|
| 6 | gateway-authentication | Hermes | 40 | Add API key validation to Gateway REST/WebSocket |
| 7 | slack-webhook-endpoint | Ralph | 35 | Add POST /webhook/slack endpoint in Gateway, validate Slack signatures |

### Phase 2: Provider Parity & Copilot — 4 Items

**P0 (2 items — Copilot first):**

| ID | Work Item | Owner | Points | Description |
|---|---|---|---|---|
| 8 | copilot-provider | Farnsworth | 60 | BotNexus.Providers.Copilot project, OpenAI-compatible HTTP, OAuth device code flow |
| 9 | providers-base-shared | Fry | 40 | Extract HTTP common code (DTOs, streaming, retry) to Providers.Base |

**P1 (2 items):**

| ID | Work Item | Owner | Points | Description |
|---|---|---|---|---|
| 10 | anthropic-tool-calling | Bender | 50 | Add tool calling to Anthropic provider (feature parity with OpenAI) |
| 11 | provider-config-validation | Ralph | 15 | Schema validation for all provider configs, helpful error messages |

### Phase 3: Completeness & Scale — 5 Items

**P0 (2 items):**

| ID | Work Item | Owner | Points | Description |
|---|---|---|---|---|
| 12 | tool-dynamic-loading | Fry | 30 | Extend loader to handle Tools (like GitHub), same folder pattern |
| 13 | config-validation-all | Ralph | 20 | Validate all config sections on startup, fail fast if invalid |

**P1 (3 items):**

| ID | Work Item | Owner | Points | Description |
|---|---|---|---|---|
| 14 | cron-task-fixes | Amy | 25 | Review cron task failures, fix any regressions |
| 15 | session-manager-tests | Fry | 30 | Add integration tests for session persistence across restarts |
| (future) | ProviderRegistry evaluation | Fry | TBD | Integrate ProviderRegistry into DI or remove dead code |

### Phase 4: Scale & Harden — 8+ Items

**P0 (2 items):**

| ID | Work Item | Owner | Points | Description |
|---|---|---|---|---|
| 16 | observability-metrics | Hermes | 40 | Add .NET metrics (tool calls, agent loops, provider latency) |
| 17 | config-documentation | Ralph | 25 | Document appsettings.json structure, env var overrides, examples |

**P1 (6+ items):**

| ID | Work Item | Owner | Points | Description |
|---|---|---|---|---|
| 18 | gateway-logging-structured | Amy | 30 | Structured logging via Serilog, trace correlation across channels |
| 19 | api-health-endpoint | Hermes | 20 | GET /health checks all providers, channels, MCP servers |
| 20 | assembly-hot-reload | Farnsworth | 35 | Research & prototype AssemblyLoadContext unload for hot-reload |
| 21 | iac-containerization | Ralph | 30 | Dockerfile, docker-compose.yml for easy deployment |
| 22 | integration-tests-e2e | Fry | 50 | Full E2E flow tests: config load → Copilot auth → agent loop → response |
| 23 | roadmap-next-quarter | Leela | 25 | Plan Q2 features (multi-agent coordination, tool chains, etc) |

---

## Part 5: Prioritization & Release Plan

**Release 1 (Foundation):** Phase 1 P0 + P1 (items 1-7)
- Enables dynamic loading, clears Copilot path, foundational auth

**Release 2 (Copilot Ready):** Phase 2 P0 (items 8-9)
- Copilot works end-to-end with OAuth device code flow

**Release 3 (Feature Parity):** Phase 2 P1 + Phase 3 P0 (items 10-13)
- All providers on equal footing, tool extensibility ready

**Release 4 (Hardened):** Phase 3 P1 + Phase 4 (items 14-23)
- Production-ready, observable, documented, containerized

---

## Part 6: Conventional Commits Requirement

All commits must follow conventional commit format:
- `feat:` New feature
- `fix:` Bug fix
- `refactor:` Code refactor (no feature change)
- `docs:` Documentation only
- `test:` Test additions
- `chore:` Build, CI, dependency updates

Each work item = one or more commits, each commit tagged with affected area (e.g., `feat(providers): add copilot oauth flow`)

Granular history makes it easy to see what changed and roll back if needed.

---

## Summary of Decisions

1. ✅ **Dynamic loading is foundation** — all work builds on it (user directive 2a)
2. ✅ **Copilot is P0 with OAuth** — device code flow, OpenAI-compatible API (user directives 2c, decision 4)
3. ✅ **Configuration-driven discovery** — dictionary-based config, no hard-coded types
4. ✅ **Conventional commits required** — feat/fix/refactor/docs/test/chore format (user directive 2b)
5. ✅ **24-item roadmap** across 4 releases with team assignments
6. ✅ **OAuth abstractions in Core** — IOAuthProvider, IOAuthTokenStore for extensible auth

**Ready for implementation.** First work: Farnsworth starts on provider-dynamic-loading (Phase 1 P0 item 1).

---

### 5. Sprint 1 Completion — 7 Foundation Items Done (2026-04-01T17:33Z)

**Status:** Complete  
**Completed by:** Farnsworth (5 items), Bender (2 items)  

All Phase 1 P0 foundation work delivered:

1. ✅ **config-model-refactor** (Farnsworth, 5c6f777)
   - Dictionary-based provider/channel config
   - Case-insensitive key matching via `StringComparer.OrdinalIgnoreCase`
   - Enables configuration-driven extension discovery

2. ✅ **extension-registrar-interface** (Farnsworth)
   - `IExtensionRegistrar` contract in Core.Abstractions
   - Extensions provide own registration logic
   - Loader discovers and invokes registrars automatically

3. ✅ **oauth-core-abstractions** (Farnsworth, 96c2c08)
   - `IOAuthProvider`, `IOAuthTokenStore`, `OAuthToken` in Core.OAuth
   - Default: encrypted file storage at `~/.botnexus/tokens/{providerName}.json`
   - Integrated with ExtensionLoader auth discriminator

4. ✅ **fix-sync-over-async** (Farnsworth)
   - Removed `MessageBusExtensions.Publish()` sync-over-async wrapper
   - All message bus publishing now fully async
   - Eliminates deadlock hazard

5. ✅ **provider-registry-integration** (Farnsworth, 4cfd246)
   - ProviderRegistry now DI-registered
   - Runtime provider resolution by model/provider key
   - Eliminates dead code, enables multi-provider dispatch

6. ✅ **fix-runner-dispatch** (Bender)
   - `IAgentRouter` injectable routing layer in Gateway
   - Multi-agent routing: metadata-driven (`agent`, `agent_name`), broadcast support (`all`, `*`)
   - `IAgentRunner.AgentName` enables deterministic routing
   - Config: `DefaultAgent`, `BroadcastWhenAgentUnspecified`

7. ✅ **dynamic-assembly-loader** (Bender, 8fe66db)
   - `ExtensionLoader` in Core + `ExtensionLoaderExtensions.AddBotNexusExtensions()`
   - Configuration-driven discovery: `BotNexus:Providers`, `BotNexus:Channels:Instances`, `BotNexus:Tools:Extensions`
   - Folder convention: `{ExtensionsPath}/{type}/{key}/`
   - One collectible `AssemblyLoadContext` per extension (isolation, future hot-reload)
   - Registrar-first, fallback to convention registration (`ILlmProvider`, `IChannel`, `ITool`)
   - Path validation (reject rooted paths, `.`/`..`, traversal)
   - Comprehensive logging, continues on missing/empty folders

**Build Status:** ✅ Green, all tests passing, 0 errors

**Unblocks:** Phase 2 P0 — Copilot Provider (item 8, Farnsworth 60pt), Providers Base (item 9, Fry 40pt)

---

## Part 4: GitHub Copilot Provider Implementation (Sprint 2, 2026-04-01T17:45Z)

**Decision:** Implement GitHub Copilot as a first-class LLM provider extension using OAuth device code flow with OpenAI-compatible chat completion API.

**Rationale:**
- Copilot is the only provider Jon uses (P0 priority per directive 2c)
- OAuth device code flow aligns with Nanobot reference pattern
- OpenAI-compatible HTTP layer reduces duplication vs. dedicated protocol
- Extension-based delivery leverages dynamic loading infrastructure

**Implementation Delivered:**

1. ✅ **BotNexus.Providers.Copilot** extension project
   - Target: `net10.0`
   - Extension metadata: `providers/copilot`
   - Imports `Extension.targets` for automatic build/publish pipeline

2. ✅ **CopilotProvider : LlmProviderBase, IOAuthProvider**
   - Base URL: `https://api.githubcopilot.com` (configurable)
   - OpenAI-compatible request/response DTOs
   - Non-streaming chat completions
   - SSE streaming with delta parsing
   - Tool call parsing (`tool_calls`)

3. ✅ **OAuth Device Code Flow (GitHubDeviceCodeFlow)**
   - `POST /login/device/code` with `scope=copilot`
   - Displays `verification_uri` + `user_code` to user
   - Polls `POST /login/oauth/access_token` until success/error/timeout
   - Token cached via IOAuthTokenStore

4. ✅ **FileOAuthTokenStore**
   - Encrypted JSON persistence
   - Default location: `%USERPROFILE%\.botnexus\tokens\{provider}.json`
   - Supports token refresh and expiry re-authentication

5. ✅ **CopilotExtensionRegistrar**
   - Binds `CopilotConfig` from `BotNexus:Providers:copilot`
   - Registers `CopilotProvider` as `ILlmProvider`
   - Registers `FileOAuthTokenStore` as default `IOAuthTokenStore` (TryAddSingleton)
   - Enables automatic DI wiring via ExtensionLoader

6. ✅ **Unit Test Coverage**
   - Chat completion scenarios
   - Streaming deltas
   - Tool calling parsing
   - Device code flow polling
   - Token caching and reuse
   - Expired token re-authentication flow

7. ✅ **Gateway Configuration Example**
   ```
   BotNexus:Providers:
     copilot:
       Enabled: true
       Auth: oauth
       DefaultModel: gpt-4o
       ApiBase: https://api.githubcopilot.com
       # Optional override:
       OAuthClientId: Iv1.b507a08c87ecfe98
   ```

**Unblocks:**
- Phase 3 work (tool extensibility, observability)
- Production deployment with Copilot as default provider
- Future OAuth pattern re-use for other providers

**Build Status:** ✅ Green, all tests passing, zero warnings

---

### 6. Sprint 3 Completion — Security & Observability Hardening (2026-04-01T18:17Z)

**Status:** Complete  
**Completed by:** Bender (3 items), Farnsworth (1 item), Hermes (2 items)  

All Phase 1-2 P1-P2 hardening and testing work delivered:

1. ✅ **api-key-auth** (Bender, 74e4085)
   - API key authentication on Gateway REST and WebSocket endpoints
   - ApiKeyAuthenticationHandler with configurable header validation
   - X-API-Key header, WebSocket query parameter fallback
   - Configuration-driven API key storage in appsettings.json

2. ✅ **extension-security** (Bender, 64c3545)
   - Assembly validation and cryptographic signature verification
   - Manifest metadata checks and dependency whitelisting
   - Configuration-driven security modes (permissive, strict)
   - Blocks untrusted code injection at extension load time

3. ✅ **observability-foundation** (Farnsworth, 7beda23)
   - Serilog structured logging integration with correlation IDs
   - Health check endpoints: /health (liveness), /health/ready (readiness)
   - Agent execution metrics: request count, latency, success rate
   - Extension loader metrics: load time, assembly count, registrar performance
   - OpenTelemetry instrumentation hooks for APM integration (Datadog, App Insights)

4. ✅ **unit-tests-loader** (Hermes, e153b67)
   - 95%+ test coverage for ExtensionLoader (50+ new test cases)
   - Comprehensive scenarios: folder discovery, validation, error handling, isolation
   - Registrar pattern verification with mock implementations
   - Performance baseline: <500ms per extension load

5. ✅ **slack-webhook-endpoint** (Bender, 9473ee7)
   - POST /api/slack/events webhook endpoint with HMAC-SHA256 signature validation
   - Slack request timestamp validation prevents replay attacks
   - Event subscription handling (url_verification)
   - Inbound message routing to Slack channel
   - Supports message, app_mention, reaction events

6. ✅ **integration-tests-extensions** (Hermes, 392f08f)
   - E2E extension loading lifecycle validation (discovery → registration → activation)
   - Multi-channel agent simulation: Discord + Slack + Telegram + WebSocket
   - Provider integration test: Copilot through dynamic loading
   - Tool execution test: GitHub tool loaded dynamically and invoked
   - Session state persistence and agent handoff validation
   - Mock channels for reproducible testing (10+ integration scenarios)

**Build Status:** ✅ Green, 140+ tests passing, 0 errors, 0 warnings

**Unblocks:** Production deployment, release validation, Sprint 4 user-facing features

---

### 7. User Directive — Multi-Agent E2E Simulation Environment (2026-04-01T18:12Z)

**By:** Jon Bullen (via Copilot CLI)  
**Status:** Captured for Sprint 4 planning

**What:** Hermes should design an E2E test environment that simulates multiple agents and channels working together. The tests should validate communication, handoff, session details, WebUI, etc. Use multiple mock channels as part of validation. Agents should use Copilot with small models — we're testing the ENVIRONMENT, not the LLM. Use simple, controlled questions that are easy to verify:
- Example: Ask note-taking agent "Quill" to list favourite pizzas
- Ask main agent "Nova" for a list of pizzas in California to try
- Tell Quill to make a list in notes for later access
- Test agent-to-agent handoff, session state, channel routing, WebUI display

The simulated environment needs a config that sets up these multi-agent scenarios with mock channels so the full flow can be validated end-to-end.

**Why:** User request — captured for team memory. This ensures BotNexus is validated as a real multi-agent platform with inter-agent communication, not just single-agent request/response.

---

### 8. User Directive — Single Config File at ~/.botnexus (2026-04-01T18:22Z)

**By:** Jon Bullen (via Copilot CLI)  
**Status:** Captured for Sprint 4 planning

**What:** All settings should be in ONE config file at `{USERPROFILE}/.botnexus/config.json` (or similar). No scattered appsettings.json files across projects. Follow the pattern used by other platforms (e.g., Nanobot uses `~/.nanobot/`). The default install location is `~/.botnexus/` with a single config file in the root for the entire environment. Extensions folder, tokens, and all runtime state live under this directory.

**Why:** User request — captured for team memory. This is an installation/deployment architecture decision that affects config loading, documentation, and the user experience.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

### 8. Cross-Document Consistency Checks as a Team Ceremony (2026-04-01T18:54Z)

**Author:** Jon Bullen (via Copilot directive)  
**Status:** Accepted  
**Related:** Leela's full consistency audit (2026-04-02)

**Context:** Jon flagged that multi-agent development causes documentation drift. When one agent changes a config path, data model, or default value, other agents (and documentation) may reference the old value. No single agent scans the entire codebase for stale references.

**Decision:** Implement consistency checks as a recurring ceremony:
- **Trigger:** After any significant change (architecture decision, config model change, path/name change)
- **Scope:** Docs matching code, docs matching each other, code comments matching behavior, README matching current state
- **Owner:** Designate \Nibbler\ (new Consistency Reviewer) to lead post-sprint audits
- **Process:** Audit cycle runs after sprint completion or architectural changes
- **Prevention:** Pull request validation should include a checklist item for consistency (when applicable)

**Why:** Critical for a platform others will learn from. Documentation is the first experience external developers have. Drift undermines trust.

**First Implementation:** Leela's audit (2026-04-02) found and fixed 22 issues across 5 files (8 in architecture.md, 3 in configuration.md, 10 in extension-development.md, 1 README rewrite, 1 code comment fix). Demonstrates scope of the problem and why a ceremony is needed.

**Team Impact:** All agents should treat consistency as a quality gate. Nibbler will formalize the process and run the recurring audits.

---

### 9. User Directive — Agent Workspace with SOUL/IDENTITY/MEMORY + Context Builder (2026-04-01T19:31Z)

**By:** Jon Bullen (via Copilot)  
**Status:** Accepted

### 2026-04-01T19:31Z: User directive — Agent workspace with SOUL/IDENTITY/MEMORY files + context builder
**By:** Jon Bullen (via Copilot)
**What:** Each agent should have a workspace folder containing personality and context files (SOUL, IDENTITY, USER, MEMORY, etc.) like OpenClaw. A context builder object should assemble the full agent context at session start from these files. Memory model should follow OpenClaw's approach: a distilled long-term memory.md file plus separate daily memory files that are searchable. Reference Nanobot's context.py for the context-building process and OpenClaw's memory tools for search and memory management.
**Why:** User request — captured for team memory. This is a fundamental agent architecture decision that defines how agents maintain identity, personality, and memory across sessions.


---

### 10. User Directive — E2E Deployment Lifecycle + Scenario Registry (2026-04-01T20:03Z)

**By:** Jon Bullen (via Copilot)  
**Status:** Accepted

### 2026-04-01T20:03Z: User directive — E2E must cover deployment lifecycle + scenario tracking process
**By:** Jon Bullen (via Copilot)
**What:** Two requirements:

1. **Scenario tracking process:** The E2E simulation scenario list must be maintained as a living document. Every time a feature is added or architecture changes, Hermes must update the scenario registry. This should be a formal process, not ad-hoc.

2. **Deployment lifecycle testing:** E2E tests must go beyond in-process testing to cover the FULL customer experience:
   - Deploying the platform (first install, config creation at ~/.botnexus/)
   - Starting the Gateway (clean start, verify health/ready)
   - Configuring agents (create workspace, set up SOUL/IDENTITY/USER files)
   - Sending messages through configured channels (Copilot provider at minimum)
   - Stopping the Gateway gracefully (verify no message loss, session persistence)
   - Restarting the Gateway (verify sessions restored, memory intact)
   - Updating the platform (add/remove extensions, config changes, restart)
   - Managing the environment (health checks, extension status, logs)
   - Integration verification (Copilot provider OAuth flow, channel routing, tool execution)
   
   Customers need to have confidence that deploying, configuring, updating, and managing BotNexus is robust and well-tested.

**Why:** User request — the platform must be tested as customers will use it, not just as code units. Deployment lifecycle is a first-class testing concern.


---

### 11. Agent Workspace, Context Builder & Memory Architecture — Implementation Plan (2026-04-02)

**Author:** Leela (Lead/Architect)  
**Status:** Accepted  
**References:** Replaces and supersedes decision #9; implementation guide for agent workspace capabilities

# Agent Workspace, Context Builder & Memory Architecture

**Author:** Leela (Lead/Architect)  
**Status:** Proposed  
**Date:** 2026-04-02  
**Requested by:** Jon Bullen  
**References:** OpenClaw workspace model, Nanobot context.py & memory.py

---

## Executive Summary

This plan adds three interconnected capabilities to BotNexus:

1. **Agent Workspaces** — per-agent folders with identity, personality, and context files
2. **Context Builder** — a new `IContextBuilder` service that assembles the full system prompt from workspace files, memory, tools, and runtime state
3. **Memory Model** — two-layer persistent memory (long-term MEMORY.md + daily files) with search, save, and consolidation

These replace the current flat `string? systemPrompt` parameter in `AgentLoop` with a rich, file-driven context system. The existing `IMemoryStore` interface is extended (not replaced) to support the new memory model. The current `ContextBuilder` class (token-budget trimmer) is refactored into the new `IContextBuilder`.

---

## Part 1: Current State Analysis

### What exists today

| Component | Location | What it does | Limitations |
|---|---|---|---|
| `AgentLoop` | `Agent/AgentLoop.cs` | Takes a flat `string? systemPrompt` in constructor; passes it to `ChatRequest` on each LLM call | No file-based context, no workspace loading, no memory injection |
| `ContextBuilder` | `Agent/ContextBuilder.cs` | Trims session history to fit context window budget (chars ≈ tokens × 4) | Only handles history trimming; no system prompt assembly, no workspace files, no memory |
| `IMemoryStore` | `Core/Abstractions/IMemoryStore.cs` | Key-value read/write/append/delete/list with `{basePath}/{agentName}/memory/{key}.txt` | No structured memory model (no MEMORY.md vs daily), no search, no consolidation |
| `MemoryStore` | `Agent/MemoryStore.cs` | File-based implementation of `IMemoryStore` | Flat key-value store; doesn't know about long-term vs daily memory |
| `AgentConfig` | `Core/Configuration/AgentConfig.cs` | Per-agent config with `SystemPrompt`, `SystemPromptFile`, `EnableMemory`, `Workspace` | No workspace file references, no identity file paths |
| `BotNexusHome` | `Core/Configuration/BotNexusHome.cs` | Manages `~/.botnexus/` structure; creates extensions/, tokens/, sessions/, logs/ | No `agents/` directory; no workspace initialization |

### Key integration points

- `AgentLoop` constructor receives `string? systemPrompt` and `ContextBuilder` — these are the insertion points
- `ChatRequest` already supports `string? SystemPrompt` — the provider layer is ready
- `IMemoryStore` is already in Core abstractions — we extend it, we don't replace it
- `BotNexusHome.Initialize()` creates the home directory structure — we add `agents/` here
- `AgentConfig.EnableMemory` already exists — we use it to gate memory features
- `ToolRegistry` accepts `ITool` implementations — memory tools register here

---

## Part 2: Agent Workspace Structure

### 2.1 Workspace Location

```
~/.botnexus/agents/{agent-name}/
```

Each named agent gets a workspace folder under `~/.botnexus/agents/`. This is separate from the existing `~/.botnexus/workspace/` (which holds sessions). The `agents/` folder is agent-specific persistent state; `workspace/` is transient session data.

**Why not `~/.botnexus/workspace/{agent-name}/`?** The existing workspace path is session-oriented. Agent identity and memory are conceptually different from session history — they persist across all sessions and channels. Clean separation avoids confusion.

### 2.2 Workspace Files

```
~/.botnexus/agents/{agent-name}/
├── SOUL.md              # Core personality, values, boundaries, communication style
├── IDENTITY.md          # Name, role, expertise descriptors, emoji/avatar
├── USER.md              # About the human: name, pronouns, timezone, preferences
├── AGENTS.md            # Multi-agent awareness: who else exists, collaboration rules
├── TOOLS.md             # Available tools and their descriptions (auto-generated)
├── HEARTBEAT.md         # Periodic task instructions (loaded by cron/heartbeat)
├── MEMORY.md            # Long-term distilled memory (loaded every session)
└── memory/              # Daily memory files
    ├── 2026-04-01.md
    ├── 2026-04-02.md
    └── ...
```

#### File Descriptions

| File | Loaded When | Authored By | Purpose |
|---|---|---|---|
| `SOUL.md` | Every session (system prompt) | Human | Core personality. "Who you are." Values, boundaries, communication style, behavioral rules. The agent's constitution. |
| `IDENTITY.md` | Every session (system prompt) | Human | Structured identity metadata: name, role title, expertise tags, emoji, avatar URL. Kept separate from SOUL for machine-parseable identity. |
| `USER.md` | Every session (system prompt) | Human + Agent | About the human operator. Name, pronouns, timezone, preferences, working style. Agent can update this as it learns about the user. |
| `AGENTS.md` | Every session (system prompt) | System (auto-generated) | Multi-agent awareness. Lists all configured agents, their roles, and collaboration protocols. Auto-generated from config + agent IDENTITY files. |
| `TOOLS.md` | Every session (system prompt) | System (auto-generated) | Describes available tools. Auto-generated from `ToolRegistry.GetDefinitions()`. Gives the agent awareness of its capabilities in natural language. |
| `HEARTBEAT.md` | On heartbeat/cron triggers | Human | Instructions for periodic tasks (health checks, memory consolidation, cleanup). Not loaded in normal sessions — only when the heartbeat service triggers. |
| `MEMORY.md` | Every session (system prompt) | Agent (via consolidation) | Distilled long-term memory. Durable facts, preferences, learned behaviors. Updated via LLM-based consolidation from daily files. |
| `memory/*.md` | Today + yesterday auto-loaded | Agent (via memory_save) | Daily running notes. Timestamped observations, conversation highlights, temporary context. Auto-loaded for today and yesterday only. |

### 2.3 Workspace Initialization

**First-run behavior:** When an agent workspace is accessed for the first time:

1. `BotNexusHome.Initialize()` gains an `InitializeAgentWorkspace(string agentName)` method
2. Creates the directory structure: `agents/{name}/`, `agents/{name}/memory/`
3. Creates stub files for human-authored files (SOUL.md, IDENTITY.md, USER.md) with placeholder content and comments explaining what to put there
4. Does NOT create AGENTS.md or TOOLS.md (these are auto-generated at context build time)
5. Creates empty MEMORY.md
6. Optionally creates HEARTBEAT.md stub if the agent has cron jobs configured

**Stub template example (SOUL.md):**
```markdown
# Soul

<!-- Define this agent's core personality, values, and boundaries.
     This file is loaded into every session as part of the system prompt.
     
     Example:
     You are a helpful, precise assistant. You value clarity and correctness.
     You communicate in a professional but warm tone. -->

(Not yet configured)
```

### 2.4 Multi-Agent Awareness (AGENTS.md)

Auto-generated at context build time from:
1. `AgentDefaults.Named` dictionary in config (gives us agent names)
2. Each agent's `IDENTITY.md` file (gives us their role/expertise)

**Generated format:**
```markdown
# Other Agents

You are part of a multi-agent system. Here are the other agents you can collaborate with:

## bender — Backend Engineer
- **Expertise:** C#, .NET, security, extension architecture
- **Ask them about:** Backend implementation, security patterns, DI wiring

## fry — Web Developer  
- **Expertise:** HTML, CSS, JavaScript, WebUI
- **Ask them about:** Frontend implementation, WebSocket client, UI

(Auto-generated from agent configurations. Do not edit manually.)
```

### 2.5 HEARTBEAT.md

**Include it.** BotNexus already has a `BotNexus.Heartbeat` project and `IHeartbeatService` in Core. The heartbeat system can load `HEARTBEAT.md` when triggering periodic agent tasks (memory consolidation, health checks, etc.). This is a natural fit.

---

## Part 3: Context Builder (`IContextBuilder`)

### 3.1 Interface Design

```csharp
namespace BotNexus.Core.Abstractions;

/// <summary>
/// Assembles the full agent context (system prompt + message history)
/// for an LLM request. Loads workspace files, memory, tools, and
/// runtime state into a structured prompt.
/// </summary>
public interface IContextBuilder
{
    /// <summary>
    /// Builds the complete system prompt from workspace files, memory,
    /// and runtime context for the given agent.
    /// </summary>
    Task<string> BuildSystemPromptAsync(
        string agentName,
        ToolRegistry toolRegistry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the trimmed message history for the LLM request,
    /// staying within the context window budget.
    /// </summary>
    IReadOnlyList<ChatMessage> BuildMessages(
        Session session,
        InboundMessage inboundMessage,
        GenerationSettings settings);
}
```

**Design rationale:**
- `BuildSystemPromptAsync` is async because it reads files from disk
- `BuildMessages` remains synchronous (operates on in-memory session data) — this is the existing `ContextBuilder.Build()` logic, relocated
- The interface is in Core so the Gateway and other modules can depend on it
- `ToolRegistry` is passed in (not injected) because it's per-agent, not a singleton

### 3.2 Implementation

New class: `AgentContextBuilder` in `BotNexus.Agent`

```csharp
namespace BotNexus.Agent;

public sealed class AgentContextBuilder : IContextBuilder
{
    // Workspace files loaded into every system prompt, in order
    private static readonly string[] BootstrapFiles = 
        ["IDENTITY.md", "SOUL.md", "USER.md", "AGENTS.md", "TOOLS.md"];
    
    private readonly string _agentsBasePath;     // ~/.botnexus/agents/
    private readonly IMemoryStore _memoryStore;
    private readonly ILogger<AgentContextBuilder> _logger;
    private readonly int _maxFileChars;          // Truncation limit per file (default 8000)
    
    public async Task<string> BuildSystemPromptAsync(
        string agentName, ToolRegistry toolRegistry, CancellationToken ct)
    {
        var parts = new List<string>();
        
        // 1. Load bootstrap workspace files
        foreach (var fileName in BootstrapFiles)
        {
            var content = await LoadWorkspaceFileAsync(agentName, fileName, ct);
            if (content is not null)
                parts.Add($"## {Path.GetFileNameWithoutExtension(fileName)}\n\n{Truncate(content)}");
        }
        
        // 2. Auto-generate TOOLS.md from ToolRegistry if no file exists
        if (!await WorkspaceFileExistsAsync(agentName, "TOOLS.md", ct))
            parts.Add(GenerateToolsSummary(toolRegistry));
        
        // 3. Load memory context
        var memoryContext = await LoadMemoryContextAsync(agentName, ct);
        if (!string.IsNullOrWhiteSpace(memoryContext))
            parts.Add($"## Memory\n\n{memoryContext}");
        
        // 4. Runtime context (date, timezone, agent name)
        parts.Add(BuildRuntimeContext(agentName));
        
        return string.Join("\n\n---\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
    
    private async Task<string> LoadMemoryContextAsync(string agentName, CancellationToken ct)
    {
        var parts = new List<string>();
        
        // Long-term memory (always loaded)
        var longTerm = await _memoryStore.ReadAsync(agentName, "MEMORY", ct);
        if (!string.IsNullOrWhiteSpace(longTerm))
            parts.Add($"### Long-term Memory\n\n{Truncate(longTerm)}");
        
        // Today's daily notes
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var todayNotes = await _memoryStore.ReadAsync(agentName, $"daily/{today}", ct);
        if (!string.IsNullOrWhiteSpace(todayNotes))
            parts.Add($"### Today ({today})\n\n{Truncate(todayNotes)}");
        
        // Yesterday's daily notes
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var yesterdayNotes = await _memoryStore.ReadAsync(agentName, $"daily/{yesterday}", ct);
        if (!string.IsNullOrWhiteSpace(yesterdayNotes))
            parts.Add($"### Yesterday ({yesterday})\n\n{Truncate(yesterdayNotes)}");
        
        return string.Join("\n\n", parts);
    }
}
```

### 3.3 Context Assembly Order

The system prompt is assembled in this order (matching Nanobot's ContextBuilder pattern):

```
┌─────────────────────────────────────────┐
│ 1. IDENTITY — Who am I?                 │
│ 2. SOUL — How do I behave?              │
│ 3. USER — Who am I talking to?          │
│ 4. AGENTS — Who else is on the team?    │
│ 5. TOOLS — What can I do?               │
│ 6. MEMORY (long-term) — What do I know? │
│ 7. MEMORY (today) — What happened today?│
│ 8. MEMORY (yesterday) — Recent context  │
│ 9. Runtime — Date, time, agent name     │
└─────────────────────────────────────────┘
```

### 3.4 Truncation Strategy

- **Per-file limit:** 8,000 characters by default (configurable via `AgentConfig.MaxContextFileChars`)
- **Total system prompt budget:** 25% of context window (e.g., 16K chars for a 64K-token context window)
- **Truncation order when over budget:** Oldest daily memory → yesterday's notes → AGENTS.md → TOOLS.md → USER.md → (SOUL and IDENTITY are never truncated)
- **Truncation indicator:** When a file is truncated, append `\n\n[... truncated, {N} chars omitted]`

### 3.5 Integration with AgentLoop

**Before (current):**
```csharp
public AgentLoop(string agentName, string? systemPrompt, ..., ContextBuilder contextBuilder, ...)
```

**After (new):**
```csharp
public AgentLoop(string agentName, IContextBuilder contextBuilder, ...) 
{
    // No more string? systemPrompt parameter
    // ContextBuilder handles everything
}

public async Task<string> ProcessAsync(InboundMessage message, CancellationToken ct)
{
    // Build system prompt once per ProcessAsync call (not per iteration)
    var systemPrompt = await _contextBuilder.BuildSystemPromptAsync(
        _agentName, _toolRegistry, ct);
    
    for (int iteration = 0; iteration < _maxToolIterations; iteration++)
    {
        var messages = _contextBuilder.BuildMessages(session, message, _settings);
        var request = new ChatRequest(messages, _settings, tools, systemPrompt);
        // ... rest unchanged
    }
}
```

**Breaking change:** The `AgentLoop` constructor signature changes. All callers (Gateway, tests) must update. The `string? systemPrompt` parameter is removed; `ContextBuilder` is replaced by `IContextBuilder`.

---

## Part 4: Memory Model

### 4.1 Two-Layer Memory

| Layer | File | Loaded | Written By | Purpose |
|---|---|---|---|---|
| Long-term | `MEMORY.md` | Every session | Consolidation (LLM) | Distilled facts, preferences, decisions. Updated by LLM summarization. The agent's "permanent record." |
| Daily | `memory/YYYY-MM-DD.md` | Today + yesterday | `memory_save` tool | Running notes and observations. Timestamped entries. Ephemeral — consolidated into MEMORY.md periodically. |

### 4.2 Memory File Format

**MEMORY.md (long-term):**
```markdown
# Memory

## User Preferences
- Jon prefers concise responses
- Always use conventional commits
- Copilot is the only LLM provider in use

## Architecture Decisions
- Extensions are dynamically loaded from ~/.botnexus/extensions/
- Config.json overrides appsettings.json
- OAuth for Copilot, API keys for other providers

## Learned Patterns
- Build: `dotnet build BotNexus.slnx`
- Test: `dotnet test BotNexus.slnx`
- 192 tests across unit, integration, E2E

(Last consolidated: 2026-04-02T14:30Z)
```

**memory/2026-04-02.md (daily):**
```markdown
# 2026-04-02

- [09:15] User asked about workspace architecture. Discussed OpenClaw reference.
- [10:30] Completed consistency audit — 22 fixes across 5 files.
- [14:00] Started workspace/memory design task. Reading codebase.
- [15:45] User confirmed HEARTBEAT.md should be included.
```

### 4.3 Memory Storage — Extending IMemoryStore

The existing `IMemoryStore` interface already supports the new model with a key convention:

| Memory Type | Key Used | Path Resolved To |
|---|---|---|
| Long-term | `"MEMORY"` | `~/.botnexus/agents/{name}/memory/MEMORY.txt` |
| Daily note | `"daily/2026-04-02"` | `~/.botnexus/agents/{name}/memory/daily/2026-04-02.txt` |
| Workspace file | (not via IMemoryStore) | `~/.botnexus/agents/{name}/SOUL.md` |

**Change needed:** The `MemoryStore` path resolution needs to move from `{basePath}/{agentName}/memory/{key}.txt` to `{agentsBasePath}/{agentName}/memory/{key}.md` (use `.md` for markdown files, and the agents base path).

We add a new `IAgentWorkspace` interface for workspace file access (SOUL.md, IDENTITY.md, etc.) separate from `IMemoryStore`:

```csharp
namespace BotNexus.Core.Abstractions;

/// <summary>
/// Provides read/write access to agent workspace files
/// (SOUL.md, IDENTITY.md, USER.md, AGENTS.md, etc.)
/// </summary>
public interface IAgentWorkspace
{
    /// <summary>Reads a workspace file for the given agent.</summary>
    Task<string?> ReadFileAsync(string agentName, string fileName, CancellationToken ct = default);
    
    /// <summary>Writes a workspace file for the given agent.</summary>
    Task WriteFileAsync(string agentName, string fileName, string content, CancellationToken ct = default);
    
    /// <summary>Checks if a workspace file exists.</summary>
    Task<bool> FileExistsAsync(string agentName, string fileName, CancellationToken ct = default);
    
    /// <summary>Lists all workspace files for the given agent.</summary>
    Task<IReadOnlyList<string>> ListFilesAsync(string agentName, CancellationToken ct = default);
    
    /// <summary>Ensures the workspace directory exists with stub files.</summary>
    Task InitializeAsync(string agentName, CancellationToken ct = default);
}
```

### 4.4 Memory Consolidation

**Trigger:** Consolidation runs when:
1. The heartbeat service fires (configurable interval, default: every 6 hours)
2. Context pressure is detected (daily notes exceed a size threshold, e.g., 10KB)
3. Manually triggered via a `memory_consolidate` tool call

**Process:**
1. Load current `MEMORY.md`
2. Load all daily files older than 2 days
3. Send to LLM with consolidation prompt:
   > "Review these daily notes and the current long-term memory. Extract durable facts, preferences, and decisions. Update the long-term memory. Discard ephemeral details."
4. LLM returns updated `MEMORY.md` content
5. Write updated `MEMORY.md`
6. Archive processed daily files (move to `memory/archive/` or delete — configurable)

**Important:** Consolidation requires an LLM call. This means the agent must have a provider configured. Consolidation should use a cheap/fast model (configurable via `AgentConfig.ConsolidationModel`).

### 4.5 Memory Search

**Phase 1 (keyword-based):** Simple grep-style search across all memory files:
```csharp
public async Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
    string agentName, string query, int maxResults = 10, CancellationToken ct = default)
{
    // 1. List all memory files (MEMORY.md + daily/*.md)
    // 2. Read each file, search for query terms (case-insensitive)
    // 3. Return matching lines with file name and line number
    // 4. Rank by recency (newer files first) and match density
}
```

**Phase 2 (hybrid, future):** Add vector embeddings stored alongside memory files. Use cosine similarity + keyword overlap for ranking. This is out of scope for the initial implementation.

---

## Part 5: Memory Tools

### 5.1 Tool: `memory_search`

```csharp
public sealed class MemorySearchTool : ToolBase
{
    public override ToolDefinition Definition => new(
        "memory_search",
        "Search across all memory files (long-term and daily notes) for relevant information.",
        new Dictionary<string, ToolParameterSchema>
        {
            ["query"] = new("string", "Search terms to find in memory", Required: true),
            ["max_results"] = new("integer", "Maximum results to return (default: 10)")
        });
}
```

### 5.2 Tool: `memory_save`

```csharp
public sealed class MemorySaveTool : ToolBase
{
    public override ToolDefinition Definition => new(
        "memory_save",
        "Save information to memory. Use 'long_term' for durable facts or 'daily' for session notes.",
        new Dictionary<string, ToolParameterSchema>
        {
            ["content"] = new("string", "The content to save", Required: true),
            ["type"] = new("string", "Memory type: 'long_term' or 'daily' (default: 'daily')"),
            ["section"] = new("string", "Section header for long-term memory (e.g., 'User Preferences')")
        });
}
```

### 5.3 Tool: `memory_get`

```csharp
public sealed class MemoryGetTool : ToolBase
{
    public override ToolDefinition Definition => new(
        "memory_get",
        "Read a specific memory file or a line range from it.",
        new Dictionary<string, ToolParameterSchema>
        {
            ["file"] = new("string", "File to read: 'MEMORY' for long-term, or a date 'YYYY-MM-DD' for daily", Required: true),
            ["start_line"] = new("integer", "Start line (1-indexed, optional)"),
            ["end_line"] = new("integer", "End line (inclusive, optional)")
        });
}
```

### 5.4 Tool Registration

Memory tools are registered per-agent when `AgentConfig.EnableMemory` is `true`:

```csharp
// In AgentLoop factory or Gateway wiring
if (agentConfig.EnableMemory == true)
{
    toolRegistry.Register(new MemorySearchTool(memoryStore, agentName, logger));
    toolRegistry.Register(new MemorySaveTool(memoryStore, agentName, logger));
    toolRegistry.Register(new MemoryGetTool(memoryStore, agentName, logger));
}
```

---

## Part 6: Configuration Changes

### 6.1 AgentConfig Additions

```csharp
public class AgentConfig
{
    // ... existing properties ...
    
    // NEW: Workspace configuration
    public string? WorkspacePath { get; set; }           // Override agent workspace path
    public int MaxContextFileChars { get; set; } = 8000; // Per-file truncation limit
    public string? ConsolidationModel { get; set; }      // Model for memory consolidation
    public int MemoryConsolidationIntervalHours { get; set; } = 6;
    public bool AutoLoadMemory { get; set; } = true;     // Auto-load MEMORY.md + daily
}
```

### 6.2 BotNexusHome Changes

```csharp
public static class BotNexusHome
{
    public static string Initialize()
    {
        // ... existing directory creation ...
        
        // NEW: Create agents directory
        Directory.CreateDirectory(Path.Combine(homePath, "agents"));
        
        return homePath;
    }
    
    /// <summary>Creates workspace structure for a specific agent.</summary>
    public static void InitializeAgentWorkspace(string agentName)
    {
        var agentPath = Path.Combine(ResolveHomePath(), "agents", agentName);
        Directory.CreateDirectory(agentPath);
        Directory.CreateDirectory(Path.Combine(agentPath, "memory"));
        Directory.CreateDirectory(Path.Combine(agentPath, "memory", "daily"));
        
        // Create stub files if they don't exist
        CreateStubIfMissing(Path.Combine(agentPath, "SOUL.md"), SoulStub);
        CreateStubIfMissing(Path.Combine(agentPath, "IDENTITY.md"), IdentityStub);
        CreateStubIfMissing(Path.Combine(agentPath, "USER.md"), UserStub);
        CreateStubIfMissing(Path.Combine(agentPath, "MEMORY.md"), "# Memory\n");
    }
}
```

### 6.3 Example Configuration

```json
{
  "BotNexus": {
    "Agents": {
      "Named": {
        "assistant": {
          "Name": "assistant",
          "EnableMemory": true,
          "Model": "copilot:gpt-4o",
          "MaxContextFileChars": 8000,
          "ConsolidationModel": "copilot:gpt-4o-mini",
          "MemoryConsolidationIntervalHours": 6
        }
      }
    }
  }
}
```

---

## Part 7: Updated Home Directory Structure

```
~/.botnexus/
├── config.json
├── extensions/
│   ├── providers/
│   ├── channels/
│   └── tools/
├── tokens/
├── sessions/              # Existing session JSONL files
├── logs/
└── agents/                # NEW: Per-agent workspaces
    ├── assistant/
    │   ├── SOUL.md
    │   ├── IDENTITY.md
    │   ├── USER.md
    │   ├── AGENTS.md       (auto-generated)
    │   ├── TOOLS.md        (auto-generated)
    │   ├── HEARTBEAT.md
    │   ├── MEMORY.md
    │   └── memory/
    │       ├── daily/
    │       │   ├── 2026-04-01.md
    │       │   └── 2026-04-02.md
    │       └── archive/    (consolidated daily files)
    └── reviewer/
        ├── SOUL.md
        ├── ...
        └── memory/
            └── daily/
```

---

## Part 8: Work Items

### Phase 1: Foundation (Core Interfaces & Config)

| ID | Title | Size | Owner | Dependencies | Description |
|---|---|---|---|---|---|
| `ws-01` | `IContextBuilder` interface in Core | S | Leela | — | Add `IContextBuilder` to `Core/Abstractions/`. Async `BuildSystemPromptAsync` + sync `BuildMessages`. |
| `ws-02` | `IAgentWorkspace` interface in Core | S | Leela | — | Add `IAgentWorkspace` to `Core/Abstractions/`. Read/write/list workspace files, initialize stubs. |
| `ws-03` | `AgentConfig` workspace properties | S | Farnsworth | — | Add `MaxContextFileChars`, `ConsolidationModel`, `MemoryConsolidationIntervalHours`, `AutoLoadMemory` to `AgentConfig`. |
| `ws-04` | `BotNexusHome` agents directory | S | Farnsworth | — | Add `agents/` to `Initialize()`. Add `InitializeAgentWorkspace(agentName)` method. |
| `ws-05` | `MemoryStore` path migration | S | Farnsworth | `ws-04` | Update `MemoryStore` to use `~/.botnexus/agents/{name}/memory/` path. Support `.md` extensions. Support `daily/` subdirectory for dated keys. |

### Phase 2: Implementation

| ID | Title | Size | Owner | Dependencies | Description |
|---|---|---|---|---|---|
| `ws-06` | `AgentWorkspace` implementation | M | Bender | `ws-02`, `ws-04` | Implement `IAgentWorkspace`. File I/O for workspace files under `~/.botnexus/agents/{name}/`. Stub file creation on init. |
| `ws-07` | `AgentContextBuilder` implementation | L | Bender | `ws-01`, `ws-02`, `ws-05`, `ws-06` | Implement `IContextBuilder`. Load bootstrap files, auto-generate AGENTS.md/TOOLS.md, load memory, build runtime context. Truncation logic. Relocate existing history-trimming from `ContextBuilder`. |
| `ws-08` | `AgentLoop` refactor | M | Bender | `ws-07` | Replace `string? systemPrompt` + `ContextBuilder` with `IContextBuilder`. Build system prompt via `BuildSystemPromptAsync`. Update all callers. |
| `ws-09` | `MemorySearchTool` | M | Bender | `ws-05` | `ITool` implementation. Grep-based search across MEMORY.md + daily files. Case-insensitive. Results ranked by recency. |
| `ws-10` | `MemorySaveTool` | S | Bender | `ws-05` | `ITool` implementation. Writes to MEMORY.md (append to section) or daily file (append timestamped entry). |
| `ws-11` | `MemoryGetTool` | S | Bender | `ws-05` | `ITool` implementation. Reads MEMORY.md or a daily file by date. Optional line range. |
| `ws-12` | Memory tool registration | S | Bender | `ws-09`, `ws-10`, `ws-11` | Register memory tools in `ToolRegistry` when `EnableMemory` is true. Wire in Gateway/AgentLoop factory. |
| `ws-13` | DI registration | M | Farnsworth | `ws-06`, `ws-07` | Add `AddAgentWorkspace()` and `AddAgentContextBuilder()` DI extension methods. Wire `IAgentWorkspace`, `IContextBuilder` into `ServiceCollection`. |

### Phase 3: Memory Consolidation

| ID | Title | Size | Owner | Dependencies | Description |
|---|---|---|---|---|---|
| `ws-14` | `IMemoryConsolidator` interface | S | Leela | `ws-05` | Interface for LLM-based memory consolidation. `ConsolidateAsync(agentName)` method. |
| `ws-15` | `MemoryConsolidator` implementation | L | Bender | `ws-14`, `ws-07` | Loads MEMORY.md + old daily files, calls LLM with consolidation prompt, writes updated MEMORY.md, archives processed dailies. |
| `ws-16` | Heartbeat consolidation trigger | M | Farnsworth | `ws-15` | Integrate consolidation with `IHeartbeatService`. Trigger based on `MemoryConsolidationIntervalHours`. Load HEARTBEAT.md for additional instructions. |

### Phase 4: Testing

| ID | Title | Size | Owner | Dependencies | Description |
|---|---|---|---|---|---|
| `ws-17` | `AgentContextBuilder` unit tests | M | Hermes | `ws-07` | Test prompt assembly, truncation, file loading, auto-generation. Mock `IAgentWorkspace` and `IMemoryStore`. |
| `ws-18` | `AgentWorkspace` unit tests | M | Hermes | `ws-06` | Test file CRUD, initialization stubs, directory creation. Use temp directories. |
| `ws-19` | Memory tools unit tests | M | Hermes | `ws-09`, `ws-10`, `ws-11` | Test search, save, get tools. Mock `IMemoryStore`. Verify tool definitions match expected schema. |
| `ws-20` | `MemoryConsolidator` unit tests | M | Hermes | `ws-15` | Test consolidation flow. Mock LLM provider. Verify MEMORY.md updates and daily file archival. |
| `ws-21` | Integration tests | L | Hermes | `ws-08`, `ws-12`, `ws-13` | End-to-end: configure agent → initialize workspace → process message → verify context includes workspace files and memory → verify memory tools work. |

### Phase 5: Documentation

| ID | Title | Size | Owner | Dependencies | Description |
|---|---|---|---|---|---|
| `ws-22` | Workspace & memory docs | M | Leela | `ws-21` | Document workspace file format, memory model, configuration options, and tool usage in `docs/`. Update architecture.md. |

### Dependency Graph

```
Phase 1 (Foundation):
  ws-01 ─┐
  ws-02 ─┼──→ Phase 2
  ws-03 ─┤
  ws-04 ─┤
  ws-05 ─┘

Phase 2 (Implementation):
  ws-06 ──→ ws-07 ──→ ws-08
  ws-09 ─┐
  ws-10 ─┼──→ ws-12 ──→ ws-13
  ws-11 ─┘

Phase 3 (Consolidation):
  ws-14 ──→ ws-15 ──→ ws-16

Phase 4 (Testing):
  ws-17, ws-18, ws-19, ws-20, ws-21 (parallel after their deps)

Phase 5 (Docs):
  ws-22 (after Phase 4)
```

### Summary

| Phase | Items | Total Size | Key Deliverables |
|---|---|---|---|
| 1. Foundation | 5 | 5×S = ~2-3 days | Interfaces, config, home directory |
| 2. Implementation | 8 | 2S+4M+1L+1S = ~5-7 days | Working context builder, memory tools, AgentLoop refactor |
| 3. Consolidation | 3 | 1S+1L+1M = ~3-4 days | LLM-based memory consolidation via heartbeat |
| 4. Testing | 5 | 4M+1L = ~4-5 days | Full test coverage |
| 5. Docs | 1 | 1M = ~1-2 days | User-facing documentation |
| **Total** | **22** | | **~15-21 days** |

---

## Part 9: Open Questions

1. **Workspace file format:** Should IDENTITY.md use structured YAML frontmatter (for machine-parseability) or freeform markdown?
   - **Recommendation:** Freeform markdown. Keep it simple. Machine-parseable metadata belongs in `config.json`, not workspace files.

2. **Memory consolidation model:** Should consolidation use the agent's configured provider, or a dedicated cheap model?
   - **Recommendation:** Configurable via `ConsolidationModel`. Default to agent's provider if not set.

3. **Daily file retention:** How long do we keep daily files after consolidation?
   - **Recommendation:** Move to `memory/archive/` for 30 days, then delete. Configurable.

4. **AGENTS.md generation frequency:** Generate at every session start, or cache and regenerate on config change?
   - **Recommendation:** Generate at session start. It's cheap (small file) and ensures freshness.

5. **Backward compatibility:** The `AgentConfig.SystemPrompt` and `SystemPromptFile` properties exist today. Do we keep them?
   - **Recommendation:** Yes. If `SystemPrompt` or `SystemPromptFile` is set, prepend it to the assembled context. This preserves backward compatibility and allows simple agents that don't need workspace files.

---

## Decision Log

| Date | Decision | Rationale |
|---|---|---|
| 2026-04-02 | Agent workspaces at `~/.botnexus/agents/{name}/`, not `~/.botnexus/workspace/{name}/` | Clean separation: agents (identity/memory) vs workspace (sessions). Different lifecycles. |
| 2026-04-02 | Extend `IMemoryStore`, don't replace it | Existing interface supports key-value model. Daily files are just keys like `daily/2026-04-02`. No breaking change. |
| 2026-04-02 | New `IAgentWorkspace` interface for workspace files | Workspace files (SOUL.md, IDENTITY.md) are conceptually different from memory. Different access patterns. |
| 2026-04-02 | New `IContextBuilder` replaces flat `string? systemPrompt` | Central place for context assembly. Enables file-driven personality, memory injection, tool awareness. |
| 2026-04-02 | Include HEARTBEAT.md | BotNexus already has heartbeat infrastructure. Natural fit for periodic memory consolidation. |
| 2026-04-02 | AGENTS.md auto-generated from config | Prevents staleness. Multi-agent awareness stays in sync with actual agent configuration. |
| 2026-04-02 | Keyword search first, hybrid later | YAGNI. Grep-based search is sufficient for initial deployment. Vector search is a future enhancement. |
| 2026-04-02 | Preserve `SystemPrompt`/`SystemPromptFile` backward compat | Simple agents shouldn't need workspace files. Inline prompts are a valid configuration path. |

