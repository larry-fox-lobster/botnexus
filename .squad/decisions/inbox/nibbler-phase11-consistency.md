## Phase 11 Consistency Review

**By:** Nibbler (Consistency Reviewer)
**Date:** 2026-07-18
**Grade:** Good

### Summary

Reviewed Phase 11 changes across 7 focus areas: new module READMEs, CLI docs, extension loading docs, config schema docs, Telegram docs, XML doc comments, and dev loop docs. Found 0 P0, 12 P1 (all fixed), and 5 P2 (noted for backlog).

Code quality remains excellent — all issues were documentation-only. The systemic pattern continues: new code ships with accurate internal structure, but cross-document references and README capability claims drift from actual implementations.

---

### Fixes Applied (12 P1s)

1. **`src/gateway/BotNexus.Gateway/README.md`**: Key Types table claimed `GatewayHostBuilder` extension on `IHostBuilder` → Fixed to `AddBotNexusGateway` on `IServiceCollection` (actual code: `GatewayServiceCollectionExtensions.cs:41`).

2. **`src/gateway/BotNexus.Gateway/README.md`**: Thread Safety section claimed `AgentRegistry` uses `ReaderWriterLockSlim` → Fixed to `Lock` (C# 13 lock type, actual code: `DefaultAgentRegistry.cs:13`).

3. **`src/gateway/BotNexus.Gateway.Api/README.md`**: Activity WebSocket endpoint documented as `/api/activity` → Fixed to `/ws/activity` (actual mapping: `GatewayApiServiceCollectionExtensions.cs:47`).

4. **`src/gateway/BotNexus.Gateway.Api/README.md`**: Auth middleware section claimed query parameter `?apiKey=<key>` support → Removed. Code only supports `X-Api-Key` header and `Authorization: Bearer` token (`ApiKeyGatewayAuthHandler.cs:142-157`).

5. **`src/gateway/BotNexus.Gateway.Api/README.md`**: Controllers table missing `ConfigController` → Added `/api/config/validate` endpoint controller.

6. **`src/gateway/BotNexus.Cli/README.md`**: `agent add` options table claimed `--provider` and `--model` are "(required)" with `--enabled` default "off" → Fixed to show actual defaults: `copilot`, `gpt-4.1`, `true` (code: `AgentCommands.cs:23-25`).

7. **`src/gateway/BotNexus.Cli/README.md`**: Global Options listed `--home` → Removed. Not implemented in `Program.cs`. Only `BOTNEXUS_HOME` env var works.

8. **`src/gateway/BotNexus.Cli/README.md`**: Missing `config schema` command → Added documentation. Also added Project Structure section documenting `Commands/` directory.

9. **`src/channels/BotNexus.Channels.Telegram/README.md`**: Status was "Stub" → Updated to "Implemented". Bender's `TelegramBotApiClient` sends real messages via `SendMessageAsync`. Updated capability table: outbound sends ✅, streaming deltas ✅ (via message editing). Added `TelegramBotApiClient` to key types.

10. **`docs/cli-reference.md`**: `--provider` default shown as `github-copilot` → Fixed to `copilot`. Same Phase 10 issue — the CLI code default was fixed but `cli-reference.md` was never updated. Also fixed example output showing `provider=github-copilot`.

11. **`docs/cli-reference.md`**: Missing `config schema` command → Added full documentation with usage, options, and examples. Updated table of contents.

12. **`docs/configuration.md`**: No mention of JSON schema validation → Added "JSON Schema Validation" section documenting `botnexus-config.schema.json`, `$schema` editor integration, CLI `config schema` command, and remote validation endpoint. Updated table of contents.

### Additional Fixes (Config Examples)

13. **`docs/platform-config.example.json`**: Missing `extensions` config section → Added `extensions` with `path` and `enabled` fields.

14. **`docs/sample-config.json`**: Missing `extensions` config section → Added `extensions` with `path` and `enabled` fields.

---

### P2 Notes (for backlog)

1. **`docs/extension-development.md`** references `IExtensionRegistrar` interface throughout — this interface does not exist in code. Bender's implementation uses `IExtensionLoader` with pure reflection-based discovery via `AssemblyLoadContextExtensionLoader`. The entire `IExtensionRegistrar` pattern in docs is fictional. Full doc rewrite needed — too large for a consistency fix.

2. **`docs/architecture.md`** extension loading section (lines ~374-400) describes `IExtensionRegistrar` and old `ExtensionLoaderExtensions.cs` patterns that don't exist. Needs update to describe the actual `IExtensionLoader` → manifest → reflection discovery flow.

3. **`docs/extension-development.md`** has no documentation of `botnexus-extension.json` manifest format (required fields: `id`, `name`, `version`, `entryAssembly`, `extensionTypes`). Developers cannot build extensions without this.

4. **`docs/configuration.md`** references Discord and Slack channels in the Channels section, but no Discord or Slack implementations exist in `src/channels/`. Only Telegram, TUI, WebSocket, and Core are implemented.

5. **`docs/architecture.md`** and `docs/extension-development.md`** reference Discord/Slack as implemented channels — these don't exist in `src/channels/`. Need to update channel lists to reflect actual implementations.

---

### Patterns Observed

- **Recurring: cli-reference.md lags CLI code changes.** The `github-copilot` → `copilot` fix from Phase 10 was applied to `AgentCommands.cs` but not to `cli-reference.md`. This is the same pattern seen in Phases 3-6 where API endpoints shipped without docs updates.

- **New: README capability claims outpace implementation → then implementation outpaces README.** The Telegram README was written as "Stub" when the adapter was scaffolded, then Bender implemented full Bot API integration without updating the README. Opposite direction from the usual drift.

- **Extension docs are the biggest gap.** `extension-development.md` describes a completely different architecture (`IExtensionRegistrar`) than what exists in code (`IExtensionLoader` + manifest + reflection). This needs a dedicated rewrite task, not a consistency patch.

- **XML doc comments are excellent.** 22 public interfaces and 9+ public classes all have comprehensive XML docs. No sync/async mismatches. `IExtensionLoader` has minimal member-level docs but is otherwise clean.
