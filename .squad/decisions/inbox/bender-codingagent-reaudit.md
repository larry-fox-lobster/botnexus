# Deep Functional Re-Audit: BotNexus.CodingAgent vs pi-mono @mariozechner/pi-coding-agent

**Author:** Bender (Runtime Dev)
**Requested by:** Jon Bullen (Copilot)
**Date:** 2025-07-24
**Scope:** Runtime-critical gaps — what will actually BREAK at runtime

---

## 1. Agent Factory Wiring

**Files compared:**
- Ours: `src/coding-agent/BotNexus.CodingAgent/CodingAgent.cs` → `CreateAsync()`
- Pi-mono: `src/core/agent-session-services.ts` → `createAgentSessionServices()` / `createAgentSessionFromServices()`

### How each side creates an agent

**Pi-mono:**
1. `createAgentSessionServices()` builds cwd-bound services: AuthStorage, SettingsManager, ModelRegistry, ResourceLoader
2. ResourceLoader loads extensions, context files, skills, prompt templates
3. `createAgentSessionFromServices()` passes services + resolved model + resolved tools → `createAgentSession()`
4. Model is resolved via `ModelRegistry` which reads `models.json` and has built-in + custom providers
5. Tools are created with `createCodingTools(cwd)` or `createAllTools(cwd)` — always cwd-bound
6. System prompt is built with full tool snippets, prompt guidelines, context files, skills

**Ours:**
1. `CreateAsync()` takes config, workingDirectory, authManager, extensionTools, skills
2. Creates tools inline, builds system prompt, resolves model
3. Wires ConvertToLlm via reflection, GetApiKey via closure, hooks via direct instantiation
4. Returns `new Agent(options)` with all delegates set

### Findings

🟢 **COSMETIC — No ResourceLoader equivalent.** Pi-mono's ResourceLoader dynamically loads context files (`.pi/` instructions, AGENTS.md, etc.) and injects them into the system prompt. We don't have this, but it's feature parity not a runtime break — agent still works.

🟢 **COSMETIC — No SettingsManager.** Pi-mono has layered settings (global → project → session). We use CodingAgentConfig with global + local layering. Functional but less flexible.

🟡 **DEGRADED — Extension provider registration missing from runtime path.** Pi-mono's `createAgentSessionServices()` calls `modelRegistry.registerProvider(name, config)` for extension-defined providers. Our `ExtensionLoader` only loads tool extensions, not provider extensions. Custom model providers from extensions won't work.
- **Fix needed:** Add provider extension loading in `Program.RegisterBuiltInProviders()` or in `CodingAgent.CreateAsync()`.

---

## 2. ConvertToLlm Delegate — Reflection Approach

**Our code** (`CodingAgent.cs:115-134`):
```csharp
private static ConvertToLlmDelegate BuildConvertToLlmDelegate()
{
    var method = Type.GetType("BotNexus.AgentCore.Loop.MessageConverter, BotNexus.AgentCore")
        ?.GetMethod("ToProviderMessages", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    if (method is null)
        throw new InvalidOperationException("MessageConverter.ToProviderMessages was not found.");
    // ...invokes via reflection
}
```

### Findings

🔴 **BLOCKER — Reflection is fragile and unnecessary.** `MessageConverter` is `internal static` in `BotNexus.AgentCore.Loop` namespace. The reflection lookup uses assembly-qualified type name which will work *only if* the assembly name matches exactly. However, this is completely unnecessary because:

1. `AgentCore` already exposes the `ConvertToLlmDelegate` delegate type publicly
2. The `AgentLoopRunner` internally calls `ContextConverter.ToProviderContext()` which already calls `ConvertToLlm`
3. The actual `MessageConverter.ToProviderMessages()` IS the correct implementation and it works

**However, the reflection IS currently working** because the assembly name matches. The risk is:
- Assembly rename → instant crash on startup
- Method rename/signature change → instant crash
- Trimming/AOT → method stripped → crash

**Fix needed:** Either:
- (a) Make `MessageConverter.ToProviderMessages` public (or add `[InternalsVisibleTo]`), then call it directly
- (b) Add a public static factory method on `AgentCore` that returns the built-in delegate
- (c) Simply inline the conversion: `return (messages, _) => Task.FromResult(MessageConverter.ToProviderMessages(messages));`

**Pi-mono equivalent:** Pi-mono doesn't have this indirection. The `pi-agent-core` library handles message conversion internally within its agent loop. The SDK consumer never provides a `ConvertToLlm` — it's built-in.

**Severity upgrade rationale:** While it works TODAY, this is a ticking time bomb. Any refactor to AgentCore internals silently breaks CodingAgent at startup with a cryptic reflection error.

---

## 3. GetApiKey in the Loop

**Trace: our GetApiKey flow:**
1. `CodingAgent.CreateAsync()` line 67-68: `GetApiKey: async (provider, ct) => await capturedAuthManager.GetApiKeyAsync(capturedConfig, provider, ct)`
2. This delegate is stored in `AgentOptions.GetApiKey`
3. `Agent` constructor stores it in `_options`
4. On each prompt, `AgentLoopRunner.RunAsync()` is called
5. Line 168: `var streamOptions = await BuildStreamOptionsAsync(config, cancellationToken)`
6. `BuildStreamOptionsAsync()` at line 230: `var apiKey = await config.GetApiKey(config.Model.Provider, cancellationToken)`
7. Line 231-233: `if (!string.IsNullOrWhiteSpace(apiKey)) { options.ApiKey = apiKey; }`
8. `LlmClient.StreamSimple()` receives `streamOptions` with `ApiKey` set
9. Provider (e.g., `CopilotProvider.StreamSimple()`) at line 41: uses `options?.ApiKey ?? EnvironmentApiKeys.GetApiKey("github-copilot")`

### Findings

🟢 **WORKING — GetApiKey IS called before every LLM request.** The full chain is intact:
- `AgentLoopRunner` → `BuildStreamOptionsAsync()` → `config.GetApiKey()` → `AuthManager.GetApiKeyAsync()` → resolves from config/env/auth.json with auto-refresh → sets `options.ApiKey` → provider uses it

The only minor concern:

🟡 **DEGRADED — No file locking on auth.json.** Pi-mono's `AuthStorage` uses `proper-lockfile` for concurrent access (multiple pi instances refreshing tokens simultaneously). Our `AuthManager.Load()` / `Save()` uses bare `File.ReadAllText` / `File.WriteAllText` with no locking. If two BotNexus instances try to refresh OAuth tokens at the same time, both may try to exchange the same refresh token, and one may get an invalid grant error.
- **Fix needed:** Add file locking (e.g., `FileStream` with `FileShare.None`) around the load-refresh-save cycle in `AuthManager.RefreshEntryAsync()`.

---

## 4. Tool Schema Comparison

### read tool

| Property | Pi-mono | Ours | Match? |
|----------|---------|------|--------|
| name | `read` | `read` | ✅ |
| path param | `path: string` | `path: string` | ✅ |
| offset/start | `offset: number` (1-indexed line start) | `start_line: integer` | 🟡 **Different name** |
| limit/end | `limit: number` (max lines to read) | `end_line: integer` | 🟡 **Different semantics** |
| Image support | Yes (detects image MIME, returns as attachment) | No | 🟡 Missing |

🟡 **DEGRADED — Read tool param names differ from pi-mono.** Pi-mono uses `offset`/`limit`, we use `start_line`/`end_line`. Models trained on pi-mono's schema will send `offset`/`limit` and our tool will ignore them, falling back to reading the entire file. This wastes tokens but doesn't crash.
- **Fix needed:** Accept `offset`/`limit` as aliases, or rename params to match.

### write tool

| Property | Pi-mono | Ours | Match? |
|----------|---------|------|--------|
| name | `write` | `write` | ✅ |
| path | `path: string` | `path: string` | ✅ |
| content | `content: string` | `content: string` | ✅ |

🟢 **MATCH** — Schema is compatible.

### edit tool

| Property | Pi-mono | Ours | Match? |
|----------|---------|------|--------|
| name | `edit` | `edit` | ✅ |
| path | `path: string` | `path: string` | ✅ |
| replacements | `edits: [{oldText, newText}]` (array, multi-edit) | `old_str: string`, `new_str: string` (single edit) | 🔴 **Incompatible** |

🔴 **BLOCKER — Edit tool schema is incompatible.** Pi-mono's edit tool accepts an `edits[]` array with `oldText`/`newText` per entry. Our edit tool expects top-level `old_str`/`new_str` for a single replacement. When the LLM sends `{edits: [{oldText: "...", newText: "..."}]}`:
- Our `PrepareArgumentsAsync` will look for `old_str` → not found → **throws `ArgumentException`** → tool call fails
- The model may retry but will keep sending pi-mono format → repeated failures

Additionally, pi-mono has a `prepareArguments` shim that accepts legacy `oldText`/`newText` top-level args and converts them. We don't accept either format from pi-mono.

**Fix needed:** 
1. Accept `edits[]` array format (primary) with `oldText`/`newText` per entry
2. Also accept `old_str`/`new_str` as a fallback single-edit format
3. Or: accept both via a `prepareArguments` normalization step like pi-mono does

### bash/shell tool

| Property | Pi-mono | Ours | Match? |
|----------|---------|------|--------|
| name | `bash` | `shell` | 🔴 **Different name** |
| command | `command: string` | `command: string` | ✅ |
| timeout | `timeout: number` (optional, no default) | `timeout: integer` (optional, default 120) | 🟡 Minor |

🔴 **BLOCKER — Tool name mismatch: `bash` vs `shell`.** The LLM will be told the tool is called `shell` but if it's been trained on pi-mono conventions, system prompts referencing `bash`, or any pi-mono documentation, it will attempt to call `bash` → tool not found → the model's coding capabilities are crippled.

The system prompt lists tool names dynamically, so the model WILL see `shell`. But pi-mono's prompt templates, documentation references, and user habits all reference `bash`. More critically, if the system prompt or context files mention "bash" the model will be confused.

**Fix needed:** Rename tool to `bash` to match pi-mono. Or register an alias.

### grep tool

| Property | Pi-mono | Ours | Match? |
|----------|---------|------|--------|
| name | `grep` | `grep` | ✅ |
| pattern | `pattern: string` | `pattern: string` | ✅ |
| path | `path: string` (optional) | `path: string` (optional) | ✅ |
| glob filter | `glob: string` | `include: string` | 🟡 **Different name** |
| ignoreCase | `ignoreCase: boolean` | ❌ Missing | 🟡 |
| literal | `literal: boolean` | ❌ Missing | 🟡 |
| context lines | `context: number` | ❌ Missing | 🟡 |
| limit | `limit: number` | `max_results: integer` | 🟡 **Different name** |

🟡 **DEGRADED — Grep param differences.** The glob filter name differs (`glob` vs `include`), and we're missing `ignoreCase`, `literal`, `context` params. Pi-mono's grep also uses ripgrep (`rg`) as the backend for speed; ours uses .NET regex which is slower but functional.
- **Fix needed:** Add `glob` as alias for `include`. Consider adding `ignoreCase`, `literal`, `context` params.

### glob/find tool

| Property | Pi-mono | Ours | Match? |
|----------|---------|------|--------|
| name | `find` | `glob` | 🟡 **Different name** |
| pattern | `pattern: string` | `pattern: string` | ✅ |
| path | `path: string` (optional) | `path: string` (optional) | ✅ |

🟡 **DEGRADED — Name mismatch: `find` vs `glob`.** Not a blocker since tools are listed dynamically, but pi-mono docs reference `find`.

### Missing tools

🟡 **DEGRADED — No `ls` tool.** Pi-mono has a dedicated `ls` tool for directory listing. Our `read` tool can list directories (falls back to `ListDirectory`), but the model won't know to use `read` for directory listing unless told.

---

## 5. System Prompt Comparison

**Pi-mono** (`system-prompt.ts`):
- Lists available tools with one-line `promptSnippet` descriptions
- Adds per-tool `promptGuidelines` (e.g., "Use read to examine files instead of cat or sed")
- Adds generic guidelines based on available tool set
- Includes pi documentation paths (README, docs/, examples/)
- Appends project context files (`.pi/` instructions)
- Appends skills section
- Adds current date and cwd

**Ours** (`SystemPromptBuilder.cs`):
- One-liner "You are a coding assistant..."
- Lists environment (OS, cwd, git branch, git status, package manager)
- Lists tool names (just names, no descriptions)
- 4 generic guidelines
- Skills section (if any)
- Custom instructions (if any)

### Findings

🟡 **DEGRADED — System prompt is significantly thinner than pi-mono's.**
1. **No tool snippets/descriptions in prompt** — Pi-mono tells the model what each tool does (e.g., "Read file contents", "Execute bash commands"). We only list names. The model relies solely on JSON schema descriptions.
2. **No per-tool guidelines** — Pi-mono adds "Use read to examine files instead of cat or sed", "Use edit for precise changes", etc. These guide tool selection behavior.
3. **No context file loading** — Pi-mono loads `.pi/` project instructions. We don't.
4. **No documentation paths** — Pi-mono points the model at its own docs for self-referential questions.
5. **Missing current date** — Pi-mono adds `Current date: YYYY-MM-DD`. We don't.

**Fix needed:** 
- Add tool description snippets to the system prompt
- Add per-tool guidelines matching pi-mono's patterns
- Add current date
- Consider adding context file loading from `.botnexus-agent/` or `.pi/`

---

## 6. Auth Flow End-to-End

**Full trace:**

1. **User runs `/login`** → `InteractiveLoop.HandleCommandAsync()` → `authManager.LoginAsync()`
2. **OAuth device flow** → `CopilotOAuth.LoginAsync()` → gets GitHub OAuth token (`ghu_...`)
3. **Exchange for Copilot token** → `CopilotOAuth.RefreshAsync(credentials)` → calls `api.github.com/copilot_internal/v2/token` → gets session token (`tid=...`)
4. **Save to auth.json** → `AuthManager._entries["github-copilot"] = entry` → `Save()` writes to `.botnexus-agent/auth.json`
5. **Next prompt** → `agent.PromptAsync()` → `AgentLoopRunner` → `BuildStreamOptionsAsync()` → `config.GetApiKey("github-copilot", ct)`
6. **GetApiKey resolves** → `AuthManager.GetApiKeyAsync(config, "github-copilot", ct)` → checks config.ApiKey → checks env → checks auth.json → auto-refreshes if expired → returns `entry.Access`
7. **Passed to provider** → `options.ApiKey = apiKey` → `CopilotProvider.StreamSimple()` → `options.ApiKey ?? EnvironmentApiKeys.GetApiKey(...)` → uses it in `Authorization: Bearer {apiKey}`

### Findings

🟢 **WORKING — The auth chain is complete and functional.** Login → save → read → refresh → use all works.

🟡 **DEGRADED — Epoch conversion mismatch.** Our `AuthManager` stores `Expires` in milliseconds (`refreshed.ExpiresAt * 1000`). When refreshing, it converts back: `new OAuthCredentials(entry.Access, entry.Refresh, entry.Expires / 1000)`. Pi-mono's `AuthStorage` also stores OAuth `expires` as-is from the provider. The conversion dance works but is fragile — if `CopilotOAuth.ExpiresAt` is already in milliseconds (check pi-mono's OAuth lib), we'd double-multiply and tokens would "expire" far in the future, never refreshing.

**Checked:** `CopilotOAuth.RefreshAsync()` returns `ExpiresAt` — this is parsed from the Copilot token response `expires_at` field which is Unix seconds. Our code does `refreshed.ExpiresAt * 1000` to store as ms, then `entry.Expires / 1000` to convert back. **This is correct but should be documented.**

🟡 **DEGRADED — No locking** (see item 3 above).

---

## 7. Session Serialization

**Our approach** (`SessionManager.cs`):
- Uses JSONL format (one JSON line per message)
- `SerializeMessage()` wraps each `AgentMessage` in a `MessageEnvelope(Type, Payload)` discriminator
- Handles: `UserMessage` → "user", `AssistantAgentMessage` → "assistant", `ToolResultAgentMessage` → "tool", `SystemAgentMessage` → "system"
- `DeserializeMessage()` switches on `envelope.Type` to deserialize the correct concrete type

### Findings

🟡 **DEGRADED — AgentToolResult serialization may lose data.** `ToolResultAgentMessage` contains `AgentToolResult` which has `IReadOnlyList<AgentToolContent>`. Each `AgentToolContent` has `Type` (enum: Text, Image) and `Value` (string). Let's check if `System.Text.Json` can round-trip these correctly:

- `AgentToolResult` is a record with `Content` property of type `IReadOnlyList<AgentToolContent>`
- `AgentToolContent` is a record with `Type` (enum) and `Value` (string)
- `JsonSerializer` needs to be able to deserialize `IReadOnlyList<AgentToolContent>` — this works because STJ deserializes interfaces as `List<T>` for concrete element types
- The enum `AgentToolContentType` needs to serialize as string or int — default is int, which is fine for round-trip as long as enum values don't change

**Potential issue:** `AssistantAgentMessage.ToolCalls` is `IReadOnlyList<ToolCallContent>?`. `ToolCallContent` is a provider-level type from `BotNexus.Providers.Core.Models`. If it's a `record` or class with proper JSON serialization attributes, it works. If it has `JsonElement` args that need special handling, deserialization could fail silently.

Let me verify:

Looking at the `ToolCallContent` type — it's defined in `Providers.Core.Models` and contains `Id`, `Name`, and `Args` (which is a `JsonElement`). `JsonElement` round-trips correctly with `System.Text.Json` — it serializes as the raw JSON and deserializes back to `JsonElement`.

🟢 **WORKING — Session serialization is functional.** The envelope pattern with type discriminator handles polymorphic dispatch correctly. `JsonElement` fields round-trip. Records are fully serializable.

**One minor gap:** `PropertyNameCaseInsensitive = true` is set on `JsonOptions` but `WriteIndented = true` increases file size for JSONL. Not a runtime issue, just wasteful for large sessions.

---

## 8. Interactive Loop Event Handling

**Our event subscriber** (`InteractiveLoop.cs:51-74`):
```csharp
agent.Subscribe(async (@event, eventCt) =>
{
    switch (@event)
    {
        case MessageUpdateEvent update when !string.IsNullOrEmpty(update.ContentDelta):
            output.WriteAssistantText(update.ContentDelta!);
            break;
        case ToolExecutionStartEvent toolStart:
            output.WriteToolStart(toolStart.ToolName, JsonSerializer.Serialize(toolStart.Args));
            break;
        case ToolExecutionEndEvent toolEnd:
            output.WriteToolEnd(toolEnd.ToolName, !toolEnd.IsError);
            break;
        case TurnEndEvent:
            // compact + save
            break;
    }
});
```

**Pi-mono's interactive mode:**
- Uses a full TUI (terminal UI) framework (`@mariozechner/pi-tui`)
- Each tool has `renderCall()` and `renderResult()` methods for rich display
- Streaming text is displayed incrementally with syntax highlighting
- Tool results show diffs, code highlighting, truncation notices
- Supports expand/collapse of tool output
- Has keybinding system for interaction

### Findings

🟢 **WORKING — Event handling is functional for basic interactive use.** Streaming text is displayed, tool starts/ends are shown, turns are separated. Session is saved after each turn.

🟡 **DEGRADED — No streaming tool output.** Pi-mono's bash tool streams output via `onUpdate` callbacks during execution. Our event subscriber only sees `ToolExecutionStartEvent` and `ToolExecutionEndEvent` — there's no intermediate output display. For long-running shell commands, the user sees "🔧 shell: {args}" then nothing until "✅ shell" or "❌ shell". This is a UX degradation but not a functional break.

🟡 **DEGRADED — No thinking/reasoning display.** Pi-mono handles thinking blocks from reasoning models. If a reasoning model (Claude with extended thinking, o1-style) returns thinking content, we have no display path for it.

---

## Summary Table

| # | Area | Severity | Issue | Fix Needed |
|---|------|----------|-------|------------|
| 2 | ConvertToLlm | 🔴 BLOCKER | Reflection-based delegate is fragile | Make MessageConverter public or add factory method |
| 4a | Edit tool schema | 🔴 BLOCKER | `old_str`/`new_str` vs `edits[{oldText,newText}]` | Accept pi-mono's `edits[]` format |
| 4b | Shell tool name | 🔴 BLOCKER | Named `shell` instead of `bash` | Rename to `bash` |
| 3 | Auth file locking | 🟡 DEGRADED | No locking on auth.json read/write | Add file locking |
| 4c | Read tool params | 🟡 DEGRADED | `start_line`/`end_line` vs `offset`/`limit` | Accept pi-mono param names |
| 4d | Grep tool params | 🟡 DEGRADED | `include` vs `glob`, missing ignoreCase/literal/context | Add aliases and missing params |
| 4e | Glob/Find name | 🟡 DEGRADED | Named `glob` instead of `find` | Consider renaming or aliasing |
| 4f | Missing ls tool | 🟡 DEGRADED | No dedicated ls tool | Add ls tool or document read fallback |
| 5 | System prompt | 🟡 DEGRADED | Much thinner than pi-mono (no tool snippets, no guidelines, no date) | Enrich system prompt |
| 1 | Extension providers | 🟡 DEGRADED | Extension provider registration not supported | Add extension provider loading |
| 8a | Streaming tool output | 🟡 DEGRADED | No intermediate output during tool execution | Add streaming output events |
| 8b | Thinking display | 🟡 DEGRADED | No reasoning/thinking block display | Add thinking event handling |
| 6 | Auth locking | 🟡 DEGRADED | No concurrent auth.json access protection | Add file locking |
| 1b | ResourceLoader | 🟢 COSMETIC | No context file loading | Feature parity, not runtime break |
| 7 | Session serialization | 🟢 WORKING | Polymorphic dispatch works correctly | None needed |
| 3 | GetApiKey chain | 🟢 WORKING | Full chain intact, called before every LLM request | None needed |
| 6 | Auth flow | 🟢 WORKING | Login → save → refresh → use chain works | None needed |

### Critical Path: What breaks a user's first prompt?

1. ✅ Provider registered → `Program.RegisterBuiltInProviders()` calls `BuiltInModels.RegisterAll()` and registers CopilotProvider
2. ✅ Model resolved → `ResolveModel()` finds model in registry OR creates fallback with correct headers
3. ⚠️ ConvertToLlm → Reflection WORKS today but is a ticking bomb
4. ✅ GetApiKey → Called correctly before each LLM request
5. ✅ Auth → Login/refresh chain works
6. 🔴 Edit tool → Model sends pi-mono format → **our tool throws** → model retries → repeated failures
7. 🔴 Shell/bash → Model may call `bash` (from training/docs) → **tool not found**

### Recommended Fix Priority

1. **Edit tool schema** — Accept `edits[]` format (BLOCKER)
2. **Shell → bash rename** — Tool name must match (BLOCKER)
3. **ConvertToLlm reflection** — Remove reflection, expose public API (BLOCKER → prevents future breakage)
4. **Read tool param aliases** — Accept `offset`/`limit` (DEGRADED)
5. **System prompt enrichment** — Add tool snippets and guidelines (DEGRADED)
6. **Auth file locking** — Prevent concurrent refresh races (DEGRADED)
