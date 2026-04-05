# Architecture overview

BotNexus is a modular AI agent execution platform built in C#/.NET. It separates concerns into three layers — providers handle LLM communication, a core agent loop orchestrates tool-calling turns, and a coding agent layer wires everything together into a working coding assistant. This document gives you the full architectural picture before you dive into individual components.

## High-level flow

```
┌──────────┐    ┌─────────────┐    ┌───────┐    ┌───────────┐    ┌───────────┐    ┌──────────┐    ┌─────────┐
│   User   │───▶│ CodingAgent │───▶│ Agent │───▶│ AgentLoop │───▶│ LlmClient │───▶│ Provider │───▶│ LLM API │
│          │◀───│             │◀───│       │◀───│  Runner   │◀───│           │◀───│          │◀───│         │
└──────────┘    └─────────────┘    └───────┘    └───────────┘    └───────────┘    └──────────┘    └─────────┘
                      │                │              │
                      │                │              ├── StreamAccumulator
                      │                │              ├── ToolExecutor
                      │                │              └── MessageConverter
                      │                │
                      │                ├── State (messages, tools, model)
                      │                └── PendingMessageQueue (steering + follow-ups)
                      │
                      ├── SystemPromptBuilder
                      ├── Built-in Tools (read, write, edit, bash, grep, glob)
                      ├── Extensions (IExtension plugins)
                      ├── SafetyHooks (command/path validation)
                      └── SessionManager (save/resume/branch)
```

Data flows left-to-right on each request: the user's prompt passes through `CodingAgent` into `Agent`, which delegates to `AgentLoopRunner`. The loop runner calls `LlmClient`, which routes to the correct provider. The response streams back through the same chain. Along the way, the loop runner may invoke tools and repeat the cycle until the LLM finishes.

## The three layers

BotNexus separates concerns into three distinct layers. Each layer depends only on the one below it.

### Layer 1: Providers (`BotNexus.Providers.Core` + implementations)

The foundation. Handles raw communication with LLM APIs.

**Responsibilities:**

- Define the message model (`UserMessage`, `AssistantMessage`, `ToolResultMessage`)
- Define the streaming protocol (`LlmStream`, `AssistantMessageEvent`)
- Route requests to the correct provider via `ApiProviderRegistry`
- Track model metadata and pricing via `ModelRegistry`

**Key types:** `IApiProvider`, `LlmClient`, `LlmStream`, `Message`, `ContentBlock`, `LlmModel`

Each provider implementation (Anthropic, OpenAI, OpenAICompat) translates the common `Context` model into its API format, makes HTTP requests, parses SSE responses, and pushes events into an `LlmStream`. Providers accept `HttpClient` via constructor injection. `CopilotProvider` is a static utility class that provides auth helpers for Copilot routing through the standard providers.

> **Deep dive:** [Provider system](01-providers.md)

### Layer 2: Agent core (`BotNexus.AgentCore`)

The engine. Implements the agent loop — the cycle of sending context to an LLM, parsing the response, executing tools, and repeating.

**Responsibilities:**

- Manage conversation state (`AgentState`)
- Run the agent loop (`AgentLoopRunner`)
- Accumulate streaming responses (`StreamAccumulator`)
- Execute tool calls with hooks (`ToolExecutor`)
- Convert between agent and provider message formats (`MessageConverter`)
- Emit lifecycle events (`AgentEvent`)

**Key types:** `Agent`, `AgentLoopRunner`, `IAgentTool`, `AgentEvent`, `BeforeToolCallDelegate`, `AfterToolCallDelegate`

The `Agent` class is a stateful wrapper that owns the message timeline, enforces single-run concurrency, and exposes `PromptAsync` / `ContinueAsync` / `Steer` / `FollowUp` APIs. Inside, `AgentLoopRunner` drives the core turn loop: drain steering → call LLM → execute tools → repeat.

> **Deep dive:** [Agent core](02-agent-core.md)

### Layer 3: Coding agent (`BotNexus.CodingAgent`)

The application. Wires everything together into a coding assistant with file tools, shell access, safety guards, and session management.

**Responsibilities:**

- Construct the agent with all tools and configuration (`CodingAgent.CreateAsync`)
- Provide built-in tools: `read`, `write`, `edit`, `bash`, `grep`, `glob`
- Build the system prompt (`SystemPromptBuilder`)
- Manage sessions (create, save, resume, branch, compact)
- Load extensions and skills
- Enforce safety rules (`SafetyHooks`)

**Key types:** `CodingAgent`, `CodingAgentConfig`, `SessionManager`, `IExtension`, `SafetyHooks`

> **Deep dive:** [Coding agent](03-coding-agent.md)

## Dependency flow

Dependencies flow in one direction — down. The provider layer knows nothing about agents. The agent core knows nothing about coding tools or sessions.

```
BotNexus.Providers.Core             ◀── No dependencies (foundation)
    │
    ├── BotNexus.Providers.Anthropic    ◀── Depends on Core
    ├── BotNexus.Providers.OpenAI       ◀── Depends on Core
    ├── BotNexus.Providers.Copilot      ◀── Depends on Core
    └── BotNexus.Providers.OpenAICompat ◀── Depends on Core
    │
BotNexus.AgentCore                  ◀── Depends on Providers.Core
    │
BotNexus.CodingAgent                ◀── Depends on AgentCore + Providers.Core
```

> **Key takeaway:** Because dependencies are one-directional, you can use `BotNexus.AgentCore` to build any kind of agent — not just a coding agent. You can also swap provider implementations without touching the agent or coding-agent layers.

## Project structure map

### `BotNexus.Providers.Core`

```
BotNexus.Providers.Core/
├── Registry/
│   ├── IApiProvider.cs            # Provider interface contract
│   ├── ApiProviderRegistry.cs     # Thread-safe provider registry
│   ├── ModelRegistry.cs           # Model metadata + cost calculation
│   └── BuiltInModels.cs           # Pre-registered Copilot models
├── Models/
│   ├── Messages.cs                # UserMessage, AssistantMessage, ToolResultMessage
│   ├── ContentBlock.cs            # TextContent, ThinkingContent, ImageContent, ToolCallContent
│   ├── Context.cs                 # SystemPrompt + Messages + Tools
│   ├── LlmModel.cs               # Model definition (id, api, provider, pricing)
│   ├── Tool.cs                    # Tool schema for LLM
│   ├── Usage.cs                   # Token usage and cost tracking
│   ├── UserMessageContent.cs      # Union: string | ContentBlock[]
│   ├── Enums.cs                   # StopReason, ThinkingLevel, CacheRetention, Transport
│   └── ThinkingBudgets.cs         # Per-level thinking token budgets
├── Streaming/
│   ├── LlmStream.cs               # Async channel of streaming events
│   └── AssistantMessageEvent.cs   # Event hierarchy (Start, TextDelta, ToolCallEnd, Done, Error)
├── LlmClient.cs                   # Top-level client: routes to providers
├── StreamOptions.cs               # Temperature, maxTokens, caching, reasoning
├── EnvironmentApiKeys.cs          # Environment variable → API key resolution
└── Utilities/
    ├── MessageTransformer.cs      # Cross-provider message normalization
    ├── CopilotHeaders.cs          # Dynamic header building for Copilot
    └── ContextOverflowDetector.cs # Regex-based context overflow detection
```

### `BotNexus.AgentCore`

```
BotNexus.AgentCore/
├── Agent.cs                       # Main agent class: state, lifecycle, events
├── PendingMessageQueue.cs         # Thread-safe steering/follow-up queues
├── Configuration/
│   ├── AgentOptions.cs            # Full agent configuration record
│   ├── AgentInitialState.cs       # Optional initial state seed
│   └── AgentLoopConfig.cs         # Immutable loop configuration
├── Loop/
│   ├── AgentLoopRunner.cs         # The main loop: LLM → accumulate → tools → repeat
│   ├── StreamAccumulator.cs       # Streaming events → complete message
│   ├── ToolExecutor.cs            # Sequential/parallel tool execution + hooks
│   ├── MessageConverter.cs        # Agent ↔ provider message conversion
│   └── ContextConverter.cs        # AgentContext → provider Context
├── Tools/
│   └── IAgentTool.cs              # Tool interface contract
├── Hooks/
│   ├── BeforeToolCallContext.cs   # Pre-execution hook context
│   ├── BeforeToolCallResult.cs    # Allow/block decision
│   ├── AfterToolCallContext.cs    # Post-execution hook context
│   └── AfterToolCallResult.cs     # Result transformation
└── Types/
    ├── AgentMessage.cs            # UserMessage, AssistantAgentMessage, ToolResultAgentMessage
    ├── AgentEvent.cs              # All lifecycle events (AgentStart, TurnStart, MessageUpdate, etc.)
    ├── AgentEventType.cs          # Event type enum
    ├── AgentState.cs              # Mutable runtime state
    ├── AgentStatus.cs             # Idle, Running, Aborting
    ├── AgentContext.cs            # Immutable context snapshot
    ├── AgentToolResult.cs         # Normalized tool result
    ├── AgentToolContent.cs        # Text or image content
    ├── AgentToolContentType.cs    # Content type enum
    ├── AgentToolUpdateCallback.cs # Partial result callback delegate
    └── ToolExecutionMode.cs       # Sequential or Parallel
```

### `BotNexus.CodingAgent`

```
BotNexus.CodingAgent/
├── CodingAgent.cs                 # Factory: CreateAsync wires everything
├── CodingAgentConfig.cs           # Config: model, provider, limits, paths
├── SystemPromptBuilder.cs         # Dynamic system prompt construction
├── Program.cs                     # CLI entry point
├── Tools/
│   ├── ReadTool.cs                # Read files/directories with line numbers
│   ├── WriteTool.cs               # Write complete files
│   ├── EditTool.cs                # Surgical edits with fuzzy matching
│   ├── ShellTool.cs               # Shell command execution (bash/PowerShell)
│   ├── GrepTool.cs                # Regex search with context lines
│   └── GlobTool.cs                # File pattern matching
├── Extensions/
│   ├── IExtension.cs              # Extension plugin contract
│   ├── ExtensionLoader.cs         # Assembly-based extension discovery
│   ├── ExtensionRunner.cs         # Extension lifecycle orchestration
│   └── SkillsLoader.cs            # Markdown skill loading
├── Session/
│   ├── SessionManager.cs          # JSONL session persistence with DAG branching
│   ├── SessionInfo.cs             # Session metadata record
│   └── SessionCompactor.cs        # Token-aware context compaction
├── Hooks/
│   ├── SafetyHooks.cs             # Path and command validation
│   └── AuditHooks.cs              # Tool call logging and timing
├── Auth/
│   └── AuthManager.cs             # OAuth device flow + token management
└── Cli/
    └── CommandParser.cs           # CLI argument parsing
```

## Data flow summary

A typical request flows through all three layers:

1. User calls `agent.PromptAsync("Fix the bug in auth.cs")`
2. `Agent` appends the user message to its timeline and acquires the run lock
3. `AgentLoopRunner` drains any pending steering messages
4. `MessageConverter` transforms the agent timeline into provider `Message[]` format
5. `LlmClient` resolves the correct provider and starts streaming via `LlmStream`
6. The provider makes an HTTP request, parses SSE events, and pushes them into the stream
7. `StreamAccumulator` converts stream events into `AgentEvent`s (MessageStart → MessageUpdate → MessageEnd)
8. If the assistant requests tool calls, `ToolExecutor` runs them (with before/after hooks), appends results to the timeline, and the loop repeats from step 3
9. When the LLM returns with no tool calls, `AgentEndEvent` fires and the run completes

## Design principles

1. **Clean layer separation.** Each layer has a single responsibility and depends only on the layer below. You can swap out any layer without affecting the others.

2. **Records for data, classes for behavior.** Messages, events, and configuration are immutable records. Stateful components (`Agent`, registries) are classes with explicit concurrency controls.

3. **Streaming-first.** Every LLM interaction is a stream (`LlmStream`). One-shot completions are built on top of streams, not the other way around.

4. **Hooks, not inheritance.** Behavior is extended via delegate hooks (`BeforeToolCall`, `AfterToolCall`) and the `IExtension` interface, not by subclassing.

5. **Thread-safe where it matters.** Registries use `ConcurrentDictionary`. Message queues use locks. The `Agent` enforces single-run concurrency via `SemaphoreSlim`.

6. **Fail gracefully.** Extensions that throw don't crash the agent. Tools that fail produce error results that the LLM can reason about. Context overflow is detected and handled with compaction.

## What's next

- **[Provider system](01-providers.md)** — how LLM communication works
- **[Agent core](02-agent-core.md)** — how the agent loop drives everything
- **[Coding agent](03-coding-agent.md)** — how the coding agent wires it all together
- **[Building your own](04-building-your-own.md)** — build a custom agent from scratch
