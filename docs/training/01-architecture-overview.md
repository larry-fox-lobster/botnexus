# 01 — System Architecture

How BotNexus is structured, how the layers connect, and why.

## High-Level Flow

```
┌──────────┐    ┌─────────────┐    ┌───────┐    ┌───────────┐    ┌───────────┐    ┌──────────┐    ┌─────────┐
│   User   │───▶│ CodingAgent │───▶│ Agent │───▶│ AgentLoop │───▶│ LlmClient │───▶│ Provider │───▶│ LLM API │
│          │◀───│   Factory   │◀───│       │◀───│  Runner   │◀───│           │◀───│          │◀───│         │
└──────────┘    └─────────────┘    └───────┘    └───────────┘    └───────────┘    └──────────┘    └─────────┘
                      │                │              │
                      │                │              ├── StreamAccumulator
                      │                │              ├── ToolExecutor
                      │                │              └── MessageConverter
                      │                │
                      │                ├── State (messages, tools, model)
                      │                ├── PendingMessageQueue (steering)
                      │                └── PendingMessageQueue (follow-ups)
                      │
                      ├── SystemPromptBuilder
                      ├── Built-in Tools (read, write, edit, bash, grep, glob)
                      ├── Extensions (IExtension plugins)
                      ├── SafetyHooks (command/path validation)
                      └── SessionManager (save/resume/branch)
```

## The Three Layers

BotNexus separates concerns into three distinct layers. Each layer depends only on the one below it.

### Layer 1: Providers (`BotNexus.Providers.Core` + implementations)

The foundation. Handles raw communication with LLM APIs.

**Responsibilities:**
- Define the message model (`UserMessage`, `AssistantMessage`, `ToolResultMessage`)
- Define the streaming protocol (`LlmStream`, `AssistantMessageEvent`)
- Route requests to the correct provider via `ApiProviderRegistry`
- Track model metadata and pricing via `ModelRegistry`

**Key types:** `IApiProvider`, `LlmClient`, `LlmStream`, `Message`, `ContentBlock`, `LlmModel`

### Layer 2: Agent Core (`BotNexus.AgentCore`)

The engine. Implements the agent loop — the cycle of sending context to an LLM, parsing the response, executing tools, and repeating.

**Responsibilities:**
- Manage conversation state (`AgentState`)
- Run the agent loop (`AgentLoopRunner`)
- Accumulate streaming responses (`StreamAccumulator`)
- Execute tool calls with hooks (`ToolExecutor`)
- Convert between agent and provider message formats (`MessageConverter`)
- Emit lifecycle events (`AgentEvent`)

**Key types:** `Agent`, `AgentLoopRunner`, `IAgentTool`, `AgentEvent`, `BeforeToolCallDelegate`, `AfterToolCallDelegate`

### Layer 3: Coding Agent (`BotNexus.CodingAgent`)

The application. Wires everything together into a coding assistant with file tools, shell access, safety guards, and session management.

**Responsibilities:**
- Construct the agent with all tools and configuration (`CodingAgent.CreateAsync`)
- Provide built-in tools: `read`, `write`, `edit`, `bash`, `grep`, `glob`
- Build the system prompt (`SystemPromptBuilder`)
- Manage sessions (create, save, resume, branch, compact)
- Load extensions and skills
- Enforce safety rules (`SafetyHooks`)

**Key types:** `CodingAgent`, `CodingAgentConfig`, `SessionManager`, `IExtension`, `SafetyHooks`

## Dependency Flow

```
BotNexus.Providers.Core          ◀── No dependencies (foundation)
    │
    ├── BotNexus.Providers.Anthropic   ◀── Depends on Core
    ├── BotNexus.Providers.OpenAI      ◀── Depends on Core
    ├── BotNexus.Providers.Copilot     ◀── Depends on Core
    └── BotNexus.Providers.OpenAICompat ◀── Depends on Core
    │
BotNexus.AgentCore               ◀── Depends on Providers.Core
    │
BotNexus.CodingAgent             ◀── Depends on AgentCore + Providers.Core
```

> **Key Takeaway:** Dependencies flow in one direction — down. The provider layer knows nothing about agents. The agent core knows nothing about coding tools or sessions. This means you can use `BotNexus.AgentCore` to build any kind of agent, not just a coding agent.

## Project Structure Map

### `BotNexus.Providers.Core`

```
BotNexus.Providers.Core/
├── Registry/
│   ├── IApiProvider.cs          # Provider interface contract
│   ├── ApiProviderRegistry.cs   # Thread-safe provider registry
│   ├── ModelRegistry.cs         # Model metadata + cost calculation
│   └── BuiltInModels.cs         # Pre-registered Copilot models
├── Models/
│   ├── Messages.cs              # UserMessage, AssistantMessage, ToolResultMessage
│   ├── ContentBlock.cs          # TextContent, ThinkingContent, ImageContent, ToolCallContent
│   ├── Context.cs               # SystemPrompt + Messages + Tools
│   ├── LlmModel.cs              # Model definition (id, api, provider, pricing)
│   ├── Tool.cs                  # Tool schema for LLM
│   ├── Usage.cs                 # Token usage and cost tracking
│   ├── UserMessageContent.cs    # Union: string | ContentBlock[]
│   ├── Enums.cs                 # StopReason, ThinkingLevel, CacheRetention, Transport
│   └── ThinkingBudgets.cs       # Per-level thinking token budgets
├── Streaming/
│   ├── LlmStream.cs             # Async channel of streaming events
│   └── AssistantMessageEvent.cs # Event hierarchy (Start, TextDelta, ToolCallEnd, Done, Error)
├── LlmClient.cs                 # Top-level client: routes to providers
├── StreamOptions.cs             # Temperature, maxTokens, caching, reasoning
├── EnvironmentApiKeys.cs        # Environment variable → API key resolution
└── Utilities/
    ├── MessageTransformer.cs    # Cross-provider message normalization
    ├── CopilotHeaders.cs        # Dynamic header building for Copilot
    └── ContextOverflowDetector.cs # Regex-based context overflow detection
```

### `BotNexus.AgentCore`

```
BotNexus.AgentCore/
├── Agent.cs                     # Main agent class: state, lifecycle, events
├── PendingMessageQueue.cs       # Thread-safe steering/follow-up queues
├── Configuration/
│   ├── AgentOptions.cs          # Full agent configuration record
│   ├── AgentInitialState.cs     # Optional initial state seed
│   └── AgentLoopConfig.cs       # Immutable loop configuration
├── Loop/
│   ├── AgentLoopRunner.cs       # The main loop: LLM → accumulate → tools → repeat
│   ├── StreamAccumulator.cs     # Streaming events → complete message
│   ├── ToolExecutor.cs          # Sequential/parallel tool execution + hooks
│   ├── MessageConverter.cs      # Agent ↔ provider message conversion
│   └── ContextConverter.cs      # AgentContext → provider Context
├── Tools/
│   └── IAgentTool.cs            # Tool interface contract
├── Hooks/
│   ├── BeforeToolCallContext.cs  # Pre-execution hook context
│   ├── BeforeToolCallResult.cs  # Allow/block decision
│   ├── AfterToolCallContext.cs  # Post-execution hook context
│   └── AfterToolCallResult.cs   # Result transformation
└── Types/
    ├── AgentMessage.cs          # UserMessage, AssistantAgentMessage, ToolResultAgentMessage
    ├── AgentEvent.cs            # All lifecycle events (AgentStart, TurnStart, MessageUpdate, etc.)
    ├── AgentEventType.cs        # Event type enum
    ├── AgentState.cs            # Mutable runtime state
    ├── AgentStatus.cs           # Idle, Running, Aborting
    ├── AgentContext.cs           # Immutable context snapshot
    ├── AgentToolResult.cs       # Normalized tool result
    ├── AgentToolContent.cs      # Text or image content
    ├── AgentToolContentType.cs  # Content type enum
    ├── AgentToolUpdateCallback.cs # Partial result callback delegate
    └── ToolExecutionMode.cs     # Sequential or Parallel
```

### `BotNexus.CodingAgent`

```
BotNexus.CodingAgent/
├── CodingAgent.cs               # Factory: CreateAsync wires everything
├── CodingAgentConfig.cs         # Config: model, provider, limits, paths
├── SystemPromptBuilder.cs       # Dynamic system prompt construction
├── Program.cs                   # CLI entry point
├── Tools/
│   ├── ReadTool.cs              # Read files/directories with line numbers
│   ├── WriteTool.cs             # Write complete files
│   ├── EditTool.cs              # Surgical edits with fuzzy matching
│   ├── ShellTool.cs             # Shell command execution (bash/PowerShell)
│   ├── GrepTool.cs              # Regex search with context lines
│   └── GlobTool.cs              # File pattern matching
├── Extensions/
│   ├── IExtension.cs            # Extension plugin contract
│   ├── ExtensionLoader.cs       # Assembly-based extension discovery
│   ├── ExtensionRunner.cs       # Extension lifecycle orchestration
│   └── SkillsLoader.cs          # Markdown skill loading
├── Session/
│   ├── SessionManager.cs        # JSONL session persistence with DAG branching
│   ├── SessionInfo.cs           # Session metadata record
│   └── SessionCompactor.cs      # Token-aware context compaction
├── Hooks/
│   ├── SafetyHooks.cs           # Path and command validation
│   └── AuditHooks.cs            # Tool call logging and timing
├── Auth/
│   └── AuthManager.cs           # OAuth device flow + token management
└── Cli/
    └── CommandParser.cs         # CLI argument parsing
```

## Design Principles

1. **Clean layer separation.** Each layer has a single responsibility and depends only on the layer below. You can swap out any layer without affecting the others.

2. **Records for data, classes for behavior.** Messages, events, and configuration are immutable records. Stateful components (Agent, registries) are classes.

3. **Streaming-first.** Every LLM interaction is a stream (`LlmStream`). One-shot completions are built on top of streams, not the other way around.

4. **Hooks, not inheritance.** Behavior is extended via delegate hooks (`BeforeToolCall`, `AfterToolCall`) and the `IExtension` interface, not by subclassing.

5. **Thread-safe where it matters.** Registries use `ConcurrentDictionary`. Message queues use locks. The `Agent` enforces single-run concurrency via `SemaphoreSlim`.

6. **Fail gracefully.** Extensions that throw don't crash the agent. Tools that fail produce error results that the LLM can reason about. Context overflow is detected and handled with compaction.

## What's Next

- **[Provider System](02-provider-system.md)** — Deep dive into how LLM communication works
- **[Agent Core](03-agent-core.md)** — How the agent loop drives everything
- **[Coding Agent](04-coding-agent.md)** — How the coding agent wires it all together
