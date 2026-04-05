# 04 — Building a Coding Agent

The `CodingAgent` layer wires everything together — tools, extensions, safety, sessions — into a working coding assistant. This doc walks through every component.

## CodingAgent Factory: What CreateAsync Does

`CodingAgent.CreateAsync` is the single entry point. It takes configuration and returns a fully wired `Agent`.

```csharp
public static class CodingAgent
{
    public static async Task<Agent> CreateAsync(
        CodingAgentConfig config,
        string workingDirectory,
        AuthManager authManager,
        LlmClient llmClient,
        ModelRegistry modelRegistry,
        ExtensionRunner? extensionRunner = null,
        IReadOnlyList<IAgentTool>? extensionTools = null,
        IReadOnlyList<string>? skills = null);
}
```

Here's what it does, step by step:

### 1. Directory Setup

```csharp
config.EnsureDirectories(workingDirectory);
// Creates: .botnexus-agent/, .botnexus-agent/sessions/, etc.
```

### 2. Tool Creation

```csharp
var tools = new List<IAgentTool>
{
    new ReadTool(workingDirectory),
    new WriteTool(workingDirectory),
    new EditTool(workingDirectory),
    new ShellTool(workingDirectory),
    new GlobTool(workingDirectory),
    new GrepTool(workingDirectory),
};
if (extensionTools is not null)
    tools.AddRange(extensionTools);
```

### 3. Git Metadata

Fetches current branch and status for the system prompt:

```csharp
var gitBranch = await GetGitBranch(workingDirectory);
var gitStatus = await GetGitStatus(workingDirectory);
```

### 4. System Prompt Construction

```csharp
var promptBuilder = new SystemPromptBuilder();
var systemPrompt = promptBuilder.Build(new SystemPromptContext(
    WorkingDirectory: workingDirectory,
    GitBranch: gitBranch,
    GitStatus: gitStatus,
    PackageManager: DetectPackageManager(workingDirectory),
    ToolNames: tools.Select(t => t.Name).ToList(),
    Skills: skills ?? Array.Empty<string>(),
    CustomInstructions: config.Custom?.GetValueOrDefault("instructions")?.ToString(),
    ToolContributions: tools.Select(t => new ToolPromptContribution(
        t.Name, t.GetPromptSnippet(), t.GetPromptGuidelines()
    )).ToList()
));
```

### 5. Hook Setup

```csharp
var auditHooks = new AuditHooks(verbose: config.Custom?.ContainsKey("verbose") == true);
var safetyHooks = new SafetyHooks();
```

### 6. Agent Options Assembly

```csharp
return new Agent(new AgentOptions(
    InitialState: new AgentInitialState(
        SystemPrompt: systemPrompt,
        Model: resolvedModel,
        Tools: tools
    ),
    Model: resolvedModel,
    LlmClient: llmClient,
    ConvertToLlm: BuildConvertToLlmDelegate(),
    TransformContext: BuildTransformContextDelegate(compactor),
    GetApiKey: async (provider, ct) => await authManager.GetApiKeyAsync(config, ct),
    BeforeToolCall: BuildBeforeToolCallDelegate(safetyHooks, extensionRunner, config),
    AfterToolCall: BuildAfterToolCallDelegate(auditHooks, extensionRunner),
    // ...
));
```

> **Key Takeaway:** `CreateAsync` is a pure factory. It constructs everything, wires the delegates, and returns an `Agent`. The `Agent` itself has no knowledge of coding-specific concerns — all coding-agent behavior is injected through hooks, tools, and delegates.

## System Prompt Construction

`SystemPromptBuilder` assembles a dynamic system prompt from context:

```csharp
public sealed class SystemPromptBuilder
{
    public string Build(SystemPromptContext context) -> string
}
```

**Sections in the system prompt:**

1. **Opening** — "You are a coding assistant with access to tools..."
2. **Environment** — OS, working directory, Git branch/status, package manager
3. **Available Tools** — Tool list with optional snippets from `GetPromptSnippet()`
4. **Tool Guidelines** — Default guidelines + custom guidelines from `GetPromptGuidelines()`
5. **Project Context** — Context files (e.g., `.botnexus-agent/context.md`)
6. **Skills** — YAML-formatted skill blocks parsed from markdown
7. **Custom Instructions** — User-provided final instructions

### Skills

Skills are markdown files with optional YAML frontmatter:

```markdown
---
name: MySkill
description: Specialized knowledge for database queries
---

When the user asks about database queries, follow these rules:
1. Always use parameterized queries
2. Check for SQL injection patterns
```

Skills are loaded from:
1. `./AGENTS.md` (project root)
2. `./.botnexus-agent/AGENTS.md` (local config)
3. `./.botnexus-agent/skills/*.md` (alphabetically sorted)

## Built-in Tools

All tools implement `IAgentTool` and operate within the working directory.

### `read` — Read Files and Directories

```csharp
public sealed class ReadTool : IAgentTool
{
    public string Name => "read";
}
```

**Parameters:** `path` (required), `start_line` (optional), `end_line` (optional)

**Behavior:**
- **Files:** Returns content with line numbers (`1 | first line`, `2 | second line`)
- **Directories:** Recursive listing up to depth 2
- **Images:** Base64-encoded with MIME type (for multimodal models)
- **Limits:** Max 2000 lines or 50KB per read; truncated content includes continuation instructions

### `write` — Write Complete Files

```csharp
public sealed class WriteTool : IAgentTool
{
    public string Name => "write";
}
```

**Parameters:** `path` (required), `content` (required)

**Behavior:** Full-file replacement. Auto-creates parent directories. Returns byte count.

### `edit` — Surgical Edits

```csharp
public sealed class EditTool : IAgentTool
{
    public string Name => "edit";
}
```

**Parameters:** `path` (required), `edits` (array of `{oldText, newText}`)

**Behavior:**
1. **Exact match** — Tries literal string match first
2. **Fuzzy fallback** — Normalizes whitespace, Unicode quotes/dashes, and line endings
3. **Validation** — Rejects overlapping edits and ambiguous matches
4. **Preservation** — Detects and maintains original line endings (CRLF/LF)

```json
{
  "path": "src/main.cs",
  "edits": [
    { "oldText": "var x = 1;", "newText": "var x = 42;" },
    { "oldText": "// TODO", "newText": "// DONE" }
  ]
}
```

### `bash` — Shell Command Execution

```csharp
public sealed class ShellTool : IAgentTool
{
    public string Name => "bash";
}
```

**Parameters:** `command` (required), `timeout` (optional, default 120s)

**Platform behavior:**
- **Windows:** Executes via PowerShell (`-NoLogo -NoProfile -NonInteractive`)
- **Unix:** Executes via `/bin/bash -lc`

**Output format:**
```
Exit Code: 0
--- STDOUT ---
Hello world
--- STDERR ---

```

**Limits:** Output capped at 50,000 characters. Process tree killed on timeout.

### `grep` — Regex Search

```csharp
public sealed class GrepTool : IAgentTool
{
    public string Name => "grep";
}
```

**Parameters:** `pattern` (regex, required), `path`, `include` (glob), `ignore_case`, `context` (lines), `max_results`

**Features:** Regex validation, `.gitignore` filtering, binary file detection, context lines, match count limiting.

### `glob` — File Pattern Matching

```csharp
public sealed class GlobTool : IAgentTool
{
    public string Name => "glob";
}
```

**Parameters:** `pattern` (glob, required), `path` (base directory)

**Features:** Uses `Microsoft.Extensions.FileSystemGlobbing`. Filters through `.gitignore` rules. Returns sorted relative paths.

## How Tools Work

The tool lifecycle in the agent loop:

```
Model requests tool call: { "name": "read", "arguments": { "path": "README.md" } }
│
├─ ToolExecutor looks up "read" (case-insensitive)
├─ Calls ReadTool.PrepareArgumentsAsync({ "path": "README.md" })
│   └─ Returns validated args (path resolved, bounds checked)
│
├─ Calls BeforeToolCall hook (SafetyHooks.ValidateAsync)
│   └─ Checks path containment, blocked paths list
│   └─ Returns BeforeToolCallResult(Block: false)
│
├─ Emits ToolExecutionStartEvent
├─ Calls ReadTool.ExecuteAsync("call_123", validatedArgs)
│   └─ Reads file, formats with line numbers
│   └─ Returns AgentToolResult([TextContent("1 | # README\n2 | ...")])
│
├─ Calls AfterToolCall hook (AuditHooks.AuditAsync)
│   └─ Logs duration and status
│   └─ Returns null (no transformation)
│
├─ Emits ToolExecutionEndEvent
└─ Creates ToolResultAgentMessage → added to timeline
```

## Session Management

Sessions persist conversation history so agents can resume where they left off.

### Session Format

Sessions are stored as JSONL files in `.botnexus-agent/sessions/`:

```jsonl
{"type":"session_header","version":2,"sessionId":"20260404-141523-a3f2","name":"Fix auth bug","workingDirectory":"/home/user/project"}
{"type":"message","entryId":"e1","parentEntryId":null,"timestamp":"2026-04-04T14:15:23Z","message":{"type":"user","payload":{...}}}
{"type":"message","entryId":"e2","parentEntryId":"e1","timestamp":"2026-04-04T14:15:30Z","message":{"type":"assistant","payload":{...}}}
{"type":"message","entryId":"e3","parentEntryId":"e2","timestamp":"2026-04-04T14:15:31Z","message":{"type":"tool","payload":{...}}}
{"type":"metadata","timestamp":"2026-04-04T14:16:00Z","key":"leaf","value":"e3"}
```

### DAG-Based Branching

Sessions use a directed acyclic graph (DAG) structure — each message entry has an `entryId` and `parentEntryId`. This enables branching:

```
e1 (user: "Fix the bug")
├─ e2 (assistant: approach A)
│  └─ e3 (tool: read file)
│     └─ e4 (assistant: here's the fix)
│
└─ e5 (assistant: approach B)     ← Branch created by different response
   └─ e6 (tool: run tests)
```

The active branch is tracked via a `leaf` metadata entry.

### SessionManager API

```csharp
public sealed class SessionManager
{
    // Create a new session
    public async Task<SessionInfo> CreateSessionAsync(
        string workingDirectory, string name, string? parentSessionId = null);

    // Save messages to session
    public async Task SaveSessionAsync(
        SessionInfo session, IReadOnlyList<AgentMessage> messages);

    // Resume a session
    public async Task<(SessionInfo, IReadOnlyList<AgentMessage>)> ResumeSessionAsync(
        string sessionId, string workingDirectory);

    // List all sessions
    public async Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(
        string workingDirectory);

    // Branch management
    public async Task<IReadOnlyList<SessionBranchInfo>> ListBranchesAsync(
        string sessionId, string workingDirectory);

    public async Task<SessionInfo> SwitchBranchAsync(
        string sessionId, string workingDirectory,
        string leafEntryId, string? branchName = null);
}
```

### Session Compaction

When conversation history grows too large, `SessionCompactor` summarizes older messages:

```csharp
var compactor = new SessionCompactor();
var options = new SessionCompactionOptions(
    MaxContextTokens: 100_000,
    ReserveTokens: 16_384,
    KeepRecentTokens: 20_000,
    KeepRecentCount: 10,
    LlmClient: llmClient,     // For LLM-powered summarization
    Model: model
);

IReadOnlyList<AgentMessage> compacted = await compactor.CompactAsync(messages, options, ct);
```

**Algorithm:**
1. Estimate total tokens (actual usage or chars ÷ 4)
2. If within budget, return as-is
3. Find cut point: keep recent messages (≥20K tokens, ≥10 messages)
4. Summarize older messages (LLM or structural fallback)
5. Return: `[SystemMessage(summary), ...recentMessages]`

The summary includes file operations tracked throughout the conversation:

```
<read-files>
src/auth.cs
src/models/user.cs
</read-files>

<modified-files>
src/auth.cs
tests/auth_test.cs
</modified-files>
```

## Extensions and Skills

### IExtension Interface

Extensions are plugins loaded from DLL assemblies:

```csharp
public interface IExtension
{
    string Name { get; }

    // Provide additional tools
    IReadOnlyList<IAgentTool> GetTools();

    // Hook: before tool execution (return Block: true to prevent)
    ValueTask<BeforeToolCallResult?> OnToolCallAsync(
        ToolCallLifecycleContext context, CancellationToken ct);

    // Hook: after tool execution (return to transform result)
    ValueTask<AfterToolCallResult?> OnToolResultAsync(
        ToolResultLifecycleContext context, CancellationToken ct);

    // Session lifecycle
    ValueTask OnSessionStartAsync(SessionLifecycleContext context, CancellationToken ct);
    ValueTask OnSessionEndAsync(SessionLifecycleContext context, CancellationToken ct);

    // Context compaction override
    ValueTask<string?> OnCompactionAsync(
        CompactionLifecycleContext context, CancellationToken ct);

    // Request interception
    ValueTask<object?> OnModelRequestAsync(
        ModelRequestLifecycleContext context, CancellationToken ct);
}
```

### Extension Loading

```csharp
var loader = new ExtensionLoader();
var result = loader.LoadExtensions(".botnexus-agent/extensions/");
// Scans *.dll, discovers IExtension implementations, instantiates them
// Individual failures don't break other extensions
```

### Extension Orchestration

`ExtensionRunner` calls all extensions in order, with these rules:
- **OnToolCallAsync:** First `Block: true` wins (short-circuits)
- **OnToolResultAsync:** Last non-null content/details/isError wins (merges)
- **OnSessionStartAsync/EndAsync:** All called, errors caught per-extension
- **OnCompactionAsync:** First non-null summary wins
- **OnModelRequestAsync:** Chains payload through all extensions

### Skills (Markdown Instructions)

Skills are simpler than extensions — they're markdown files injected into the system prompt:

```csharp
var loader = new SkillsLoader();
IReadOnlyList<string> skills = loader.LoadSkills(workingDirectory, config);
// Loaded from: AGENTS.md, .botnexus-agent/AGENTS.md, .botnexus-agent/skills/*.md
```

Use skills for teaching the agent domain-specific knowledge. Use extensions for adding tools and hooks.

## Safety: SafetyHooks

`SafetyHooks` enforces safety rules as a `BeforeToolCall` hook:

### Path Validation (write/edit tools)

1. Resolve path with `PathUtils.ResolvePath()` — enforces containment within working directory
2. Check against `config.BlockedPaths` list
3. Warn if payload exceeds 1MB

### Shell Command Validation (bash tool)

1. If `config.AllowedCommands` is non-empty, command must start with an allowed prefix
2. Check against blocked patterns (`rm -rf /`, `format`, `del /s /q`)
3. Case-insensitive matching

```csharp
// In CodingAgentConfig:
AllowedCommands: ["dotnet", "git", "npm"],   // Only these prefixes allowed
BlockedPaths: [".env", "secrets/"]            // These paths are off-limits
```

## Configuration Hierarchy

Configuration merges in order — later layers override earlier ones:

```
1. Defaults (hardcoded)
   ├─ MaxToolIterations: 40
   ├─ MaxContextTokens: 100000
   └─ Model: "gpt-4.1", Provider: "github-copilot"

2. Global config (~/.botnexus/coding-agent.json)
   └─ User-wide preferences

3. Local config (.botnexus-agent/config.json)
   └─ Project-specific overrides

4. CLI arguments (--model, --provider)
   └─ Session-level overrides
```

**Config file format:**
```json
{
  "model": "claude-sonnet-4",
  "provider": "anthropic",
  "maxToolIterations": 80,
  "maxContextTokens": 200000,
  "allowedCommands": ["dotnet", "git"],
  "blockedPaths": [".env"],
  "custom": {
    "verbose": true,
    "instructions": "Always write tests before implementation."
  }
}
```

## What's Next

- **[Build Your Own Agent](05-building-your-own.md)** — Hands-on tutorial: custom agent, tool, and extension
- **[Add a Provider](06-adding-a-provider.md)** — Implement a new LLM provider
