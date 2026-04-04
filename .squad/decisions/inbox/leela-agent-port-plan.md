# Decision: Multi-Sprint Plan to Port pi-mono Agent into BotNexus

**Author:** Leela (Lead/Architect)  
**Date:** 2026-04-05  
**Status:** Proposed  
**Requested by:** Jon Bullen (via Copilot)

---

## Summary

Port the `@mariozechner/pi-agent-core` TypeScript package into a new standalone C#/.NET project: **`BotNexus.AgentCore`**. This is a 4-sprint effort that creates a clean, pi-mono-faithful agent loop engine referencing only `BotNexus.Providers.Base` (and transitively `BotNexus.Core`). It does NOT modify or integrate with the existing `BotNexus.Agent`.

---

## Architecture Decisions (Upfront)

### AD-1: Project Name — `BotNexus.AgentCore`

Mirrors `pi-agent-core`. The name signals "core agent loop engine" as distinct from the existing `BotNexus.Agent` which is the full pipeline (session, channels, commands, hooks, memory).

**Location:** `src/BotNexus.AgentCore/`  
**Test project:** `tests/BotNexus.AgentCore.Tests/`

### AD-2: Dependency Graph

```
BotNexus.AgentCore
  └── BotNexus.Providers.Base
        └── BotNexus.Core
```

No references to Session, Gateway, Channels, Command, or the existing Agent. This is the pi-mono boundary — the agent core depends only on the LLM abstraction layer.

### AD-3: EventStream → `ChannelReader<AgentEvent>`

pi-mono uses a custom `EventStream` (async iterator). In C#, we use `System.Threading.Channels.Channel<AgentEvent>` which gives us:
- `ChannelWriter<AgentEvent>` for the loop to emit events
- `ChannelReader<AgentEvent>` for consumers (the `Agent` class, subscribers)
- Backpressure, cancellation, and async enumeration via `ReadAllAsync()`

This is idiomatic C# and maps cleanly to pi-mono's `EventStream`.

### AD-4: AbortSignal → `CancellationToken`

All pi-mono `signal` parameters become `CancellationToken cancellationToken`. The `Agent.abort()` method uses `CancellationTokenSource.Cancel()`.

### AD-5: Event Subscription Model

pi-mono uses `subscribe(listener)` returning an unsubscribe function. In C#:
- `Agent.Subscribe(Action<AgentEvent> listener)` returns `IDisposable`
- Internally uses a concurrent subscriber list
- Disposing unsubscribes
- CancellationToken support for automatic cleanup

### AD-6: AgentEvent Hierarchy — Record Types with Discriminator

pi-mono has 10 event types as a discriminated union. In C#:

```csharp
public abstract record AgentEvent(AgentEventType Type, DateTimeOffset Timestamp);

public record AgentStartEvent(...)    : AgentEvent(AgentEventType.AgentStart, ...);
public record AgentEndEvent(...)      : AgentEvent(AgentEventType.AgentEnd, ...);
public record TurnStartEvent(...)     : AgentEvent(AgentEventType.TurnStart, ...);
public record TurnEndEvent(...)       : AgentEvent(AgentEventType.TurnEnd, ...);
public record MessageStartEvent(...)  : AgentEvent(AgentEventType.MessageStart, ...);
public record MessageUpdateEvent(...) : AgentEvent(AgentEventType.MessageUpdate, ...);
public record MessageEndEvent(...)    : AgentEvent(AgentEventType.MessageEnd, ...);
public record ToolExecutionStartEvent(...)  : AgentEvent(AgentEventType.ToolExecutionStart, ...);
public record ToolExecutionUpdateEvent(...) : AgentEvent(AgentEventType.ToolExecutionUpdate, ...);
public record ToolExecutionEndEvent(...)    : AgentEvent(AgentEventType.ToolExecutionEnd, ...);
```

Pattern matching gives us the exhaustive switch semantics that TypeScript gets from discriminated unions.

### AD-7: AgentTool — Interface with Typed Parameters

pi-mono's `AgentTool` has TypeBox schema for parameters, `prepareArguments`, and `execute`. In C#:

```csharp
public interface IAgentTool
{
    string Name { get; }
    string Label { get; }
    ToolDefinition Definition { get; }  // Reuse from Core
    Task<IReadOnlyDictionary<string, object?>> PrepareArgumentsAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);
    Task<AgentToolResult> ExecuteAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);
}
```

This extends the existing `ITool` concept with pi-mono's richer tool semantics (label, prepareArguments, structured result).

### AD-8: No Proxy Streaming

`proxy.ts` is browser-specific infrastructure for routing through a backend server. Not applicable to a server-side C#/.NET library. **Excluded from port scope.**

### AD-9: AgentMessage — Extensible via Interfaces

pi-mono uses TypeScript declaration merging for extensibility. In C#:

```csharp
public abstract record AgentMessage(string Role);
public record UserMessage(string Content) : AgentMessage("user");
public record AssistantMessage(string Content, IReadOnlyList<ToolCallRequest>? ToolCalls = null) : AgentMessage("assistant");
public record ToolResultMessage(string ToolCallId, string ToolName, AgentToolResult Result) : AgentMessage("tool");
public record SystemMessage(string Content) : AgentMessage("system");
```

Extension is via subclassing, which is the C# equivalent of declaration merging.

### AD-10: Reuse vs. New Types

| Concept | Decision |
|---------|----------|
| `ChatMessage` | **Wrap, don't replace.** `AgentMessage` is the rich type; convert to `ChatMessage` for LLM calls |
| `ChatRequest` | **Build internally.** `AgentContext` builds a `ChatRequest` when calling the LLM |
| `ToolDefinition` | **Reuse directly.** Same schema concept |
| `ILlmProvider` | **Consume directly.** The stream function IS `ChatStreamAsync` |
| `StreamingChatChunk` | **Consume directly.** Accumulate into `MessageUpdateEvent` |
| `LlmResponse` | **Consume directly.** For non-streaming fallback |
| `ModelDefinition` | **Consume directly.** The model IS a `ModelDefinition` |

---

## Sprint Plan

### Sprint 1: Foundation — Types, Interfaces & Project Scaffold

**Duration:** 1-2 days  
**Owner:** Farnsworth (Platform Dev)  
**Gate:** Leela reviews all type definitions and interfaces before Sprint 2

#### Deliverables

**1.1 Project scaffold**
- Create `src/BotNexus.AgentCore/BotNexus.AgentCore.csproj`
  - Target: `net10.0`, nullable enabled, implicit usings
  - References: `BotNexus.Providers.Base` only
  - Package: `System.Threading.Channels` (if not already in framework)
- Create `tests/BotNexus.AgentCore.Tests/BotNexus.AgentCore.Tests.csproj`
  - xUnit, FluentAssertions, Moq (match existing test project pattern)
  - Reference: `BotNexus.AgentCore`
- Add both to `BotNexus.slnx`

**1.2 Enums**
- `ThinkingLevel.cs` — `Off, Minimal, Low, Medium, High, ExtraHigh`
- `ToolExecutionMode.cs` — `Sequential, Parallel`
- `AgentEventType.cs` — All 10 event types
- `AgentStatus.cs` — `Idle, Running, Aborting`

**1.3 Core records**
- `AgentMessage.cs` — Abstract base + `UserMessage`, `AssistantMessage`, `ToolResultMessage`, `SystemMessage`
- `AgentToolResult.cs` — `record AgentToolResult(IReadOnlyList<AgentToolContent> Content, IReadOnlyDictionary<string, object?>? Details = null)`
- `AgentToolContent.cs` — `record AgentToolContent(AgentToolContentType Type, string Value)` with `enum AgentToolContentType { Text, Image }`
- `AgentEvent.cs` — Abstract base + all 10 event record types

**1.4 State & context**
- `AgentState.cs` — Mutable state class: SystemPrompt, Model, ThinkingLevel, Tools, Messages, IsStreaming, PendingToolCalls, Error
- `AgentContext.cs` — `record AgentContext(string? SystemPrompt, IReadOnlyList<AgentMessage> Messages, IReadOnlyList<IAgentTool> Tools)`

**1.5 Configuration**
- `AgentLoopConfig.cs` — Model, ConvertToLlm delegate, TransformContext delegate, GetApiKey delegate, GetSteeringMessages, GetFollowUpMessages, ToolExecutionMode, BeforeToolCall/AfterToolCall hooks
- `AgentOptions.cs` — Constructor options for the `Agent` class (superset of loop config + initial state)

**1.6 Tool interface**
- `IAgentTool.cs` — Name, Label, Definition, PrepareArgumentsAsync, ExecuteAsync
- `BeforeToolCallContext.cs` / `AfterToolCallContext.cs` — Hook context records
- `BeforeToolCallResult.cs` / `AfterToolCallResult.cs` — Hook result records (allow skip/modify)

**1.7 Delegate types**
- `Delegates.cs` — `ConvertToLlmDelegate`, `TransformContextDelegate`, `GetApiKeyDelegate`, `GetMessagesDelegate`, `BeforeToolCallDelegate`, `AfterToolCallDelegate`

#### Commit Plan
1. `feat(agent-core): scaffold BotNexus.AgentCore project and test project` — csproj files, solution update
2. `feat(agent-core): add core enums (ThinkingLevel, ToolExecutionMode, AgentEventType, AgentStatus)` — all enum files
3. `feat(agent-core): add AgentMessage hierarchy and AgentToolResult types` — message + tool result records
4. `feat(agent-core): add AgentEvent hierarchy (10 event types)` — all event records
5. `feat(agent-core): add AgentState, AgentContext, and configuration types` — state, context, config, options
6. `feat(agent-core): add IAgentTool interface, hook types, and delegates` — tool interface, hooks, delegates

---

### Sprint 2: Agent Loop — The Engine

**Duration:** 2-3 days  
**Owner:** Bender (Runtime Dev)  
**Prerequisite:** Sprint 1 gate passed  
**Gate:** Leela reviews loop correctness, event emission, and tool execution before Sprint 3

#### Deliverables

**2.1 Stream accumulator**
- `StreamAccumulator.cs` — Consumes `IAsyncEnumerable<StreamingChatChunk>` from the provider, accumulates content + tool calls, emits `MessageStart`, `MessageUpdate`, `MessageEnd` events via a `ChannelWriter<AgentEvent>`
- Handles: content deltas, tool call start/delta/finish, usage stats

**2.2 Tool executor**
- `ToolExecutor.cs` — Executes tool calls against `IAgentTool` instances
  - Sequential mode: execute one at a time, emit `ToolExecutionStart/Update/End` for each
  - Parallel mode: execute all concurrently via `Task.WhenAll`, emit events for each
  - Preflight validation: check tool exists, arguments valid
  - BeforeToolCall/AfterToolCall hook invocation
  - Handles: cancellation, errors, skip results from hooks

**2.3 Context converter**
- `ContextConverter.cs` — Converts `AgentContext` (with `AgentMessage` list) into a `ChatRequest` (with `ChatMessage` list) for the LLM provider. This is the bridge between the agent type system and the provider type system. Default implementation + pluggable via `ConvertToLlmDelegate`.

**2.4 Agent loop functions**
- `AgentLoopRunner.cs` — The core loop engine:
  - `RunAsync(prompts, context, config, cancellationToken)` → `ChannelReader<AgentEvent>`
  - `ContinueAsync(context, config, cancellationToken)` → `ChannelReader<AgentEvent>`
  - Inner loop: stream assistant response → if tool calls, execute → append results → continue
  - Outer loop: after inner loop completes, check for follow-up messages → if any, re-enter inner loop
  - Steering messages: injected between turns in the inner loop
  - Emits: `TurnStart/TurnEnd` around each inner iteration, `AgentStart/AgentEnd` around the full run

**2.5 Message conversion utilities**
- `MessageConverter.cs` — Static helpers to convert between `AgentMessage` ↔ `ChatMessage`, `AgentToolResult` → tool result string, etc.

#### Commit Plan
1. `feat(agent-core): add MessageConverter for AgentMessage ↔ ChatMessage conversion` — conversion utilities
2. `feat(agent-core): add ContextConverter for AgentContext → ChatRequest` — context bridge
3. `feat(agent-core): add StreamAccumulator for streaming response processing` — chunk → event emission
4. `feat(agent-core): add ToolExecutor with sequential and parallel modes` — tool execution engine
5. `feat(agent-core): add AgentLoopRunner — core agent loop engine` — the main loop

---

### Sprint 3: Agent Class — The Stateful Wrapper

**Duration:** 1-2 days  
**Owner:** Bender (Runtime Dev)  
**Prerequisite:** Sprint 2 gate passed  
**Gate:** Leela reviews public API surface, thread safety, and lifecycle management before Sprint 4

#### Deliverables

**3.1 Pending message queue**
- `PendingMessageQueue.cs` — Thread-safe queue for steering and follow-up messages. Supports `Enqueue`, `TryDequeueAll`, `Clear`. Used by the Agent to buffer messages that arrive while a run is active.

**3.2 Agent class**
- `Agent.cs` — The main public API, stateful wrapper around `AgentLoopRunner`:
  - **Constructor:** Takes `AgentOptions` (initial state, config, provider, etc.)
  - **Properties:** `State` (read-only snapshot), `Status` (Idle/Running/Aborting)
  - **Subscribe(listener)** → `IDisposable` — Event subscription
  - **PromptAsync(message)** — Start a new run with user input (text, AgentMessage, or batch)
  - **ContinueAsync()** — Continue from current context (no new user message)
  - **SteerAsync(message)** — Queue a steering message for the current run
  - **FollowUpAsync(message)** — Queue a follow-up message for after current run
  - **AbortAsync()** — Cancel the current run gracefully
  - **WaitForIdleAsync()** — Await until status returns to Idle
  - **Reset()** — Clear state and cancel any active run
  - **Internal lifecycle:** Manages `CancellationTokenSource`, active run `Task`, event forwarding to subscribers

**3.3 Thread safety**
- All public methods on `Agent` are thread-safe
- State mutations synchronized via `SemaphoreSlim` or lock
- Subscriber list is copy-on-write for safe iteration during event emission

#### Commit Plan
1. `feat(agent-core): add PendingMessageQueue for steering and follow-up messages` — thread-safe queue
2. `feat(agent-core): add Agent class — stateful wrapper with full public API` — the main class
3. `feat(agent-core): add thread safety and lifecycle management to Agent` — synchronization, disposal

---

### Sprint 4: Tests, Documentation & Polish

**Duration:** 2-3 days  
**Owners:** Hermes (Tester) + Kif (Documentation)  
**Gate:** Leela final review — coverage targets, API docs completeness, README accuracy

#### Test Deliverables (Hermes)

**4.1 Test utilities**
- `tests/BotNexus.AgentCore.Tests/TestUtils/CalculateTool.cs` — Simple arithmetic tool (mirrors pi-mono test utility)
- `tests/BotNexus.AgentCore.Tests/TestUtils/GetCurrentTimeTool.cs` — Time tool (mirrors pi-mono)
- `tests/BotNexus.AgentCore.Tests/TestUtils/MockLlmProvider.cs` — Configurable mock that returns predefined responses/streams
- `tests/BotNexus.AgentCore.Tests/TestUtils/TestHelpers.cs` — Factory methods for creating test configs, contexts, etc.

**4.2 Agent loop tests** (mirrors `agent-loop.test.ts`)
- `AgentLoopRunnerTests.cs`:
  - Simple text response (no tools)
  - Single tool call → result → final response
  - Multi-turn tool calls (iterative)
  - Parallel tool execution
  - Sequential tool execution
  - Cancellation mid-stream
  - BeforeToolCall hook (skip tool)
  - AfterToolCall hook (modify result)
  - Steering messages injection
  - Follow-up messages
  - Max iterations limit
  - Error handling (tool throws, LLM errors)

**4.3 Agent class tests** (mirrors `agent.test.ts`)
- `AgentTests.cs`:
  - Prompt → events emitted → idle
  - Subscribe/unsubscribe
  - Abort during active run
  - WaitForIdleAsync
  - Reset clears state
  - Steer during active run
  - FollowUp queuing
  - Continue from existing context
  - Concurrent prompt rejection (only one active run)
  - State snapshots are immutable copies

**4.4 E2E tests** (mirrors `e2e.test.ts`)
- `AgentCoreE2ETests.cs`:
  - Full prompt → tool execution → final response cycle with mock provider
  - Multi-turn conversation with tool use
  - Streaming event sequence validation

#### Documentation Deliverables (Kif)

**4.5 README**
- `src/BotNexus.AgentCore/README.md`:
  - Overview and relationship to pi-mono
  - Architecture diagram (text)
  - Quick start example
  - Type reference (key types and what they do)
  - Agent loop flow description
  - Tool creation guide
  - Event subscription guide
  - Configuration reference
  - Differences from pi-mono

**4.6 XML doc audit**
- Ensure all public types and members have `<summary>` docs
- Add `<remarks>` for non-obvious behavior
- Add `<example>` for key APIs

#### Commit Plan (Hermes)
1. `test(agent-core): add test utilities (mock provider, test tools, helpers)` — test infrastructure
2. `test(agent-core): add AgentLoopRunner unit tests` — loop tests
3. `test(agent-core): add Agent class unit tests` — wrapper tests
4. `test(agent-core): add E2E integration tests` — full flow tests

#### Commit Plan (Kif)
1. `docs(agent-core): add comprehensive README` — full documentation
2. `docs(agent-core): audit and complete XML documentation` — doc comments

---

## Risk Register

| # | Risk | Likelihood | Impact | Mitigation |
|---|------|-----------|--------|------------|
| R1 | **Type mapping gaps** — pi-mono's TypeScript types may not map cleanly to C# records | Medium | Medium | Sprint 1 gate review. Leela validates all type definitions before implementation begins. |
| R2 | **Streaming complexity** — `StreamingChatChunk` accumulation into events is the most complex piece | High | High | Sprint 2 focuses on this first. StreamAccumulator gets dedicated testing. |
| R3 | **Thread safety in Agent class** — Concurrent access patterns differ from JS single-threaded model | Medium | High | Sprint 3 explicitly addresses thread safety. SemaphoreSlim for state, copy-on-write for subscribers. |
| R4 | **Event ordering** — Events must be emitted in the same order as pi-mono to be faithful | Medium | Medium | E2E tests validate event sequence. Reference pi-mono's test assertions. |
| R5 | **ConvertToLlm bridge** — Converting AgentMessage → ChatMessage may lose information | Low | High | Sprint 2 implements MessageConverter with round-trip tests. |
| R6 | **Scope creep into BotNexus.Agent** — Temptation to integrate with existing agent pipeline | Medium | High | Hard rule: zero references from/to BotNexus.Agent. Integration is a future sprint. |
| R7 | **Channel<T> backpressure** — If consumer is slow, events could buffer unbounded | Low | Medium | Use `BoundedChannelOptions` with sensible capacity. |

---

## Open Questions

1. **Should `AgentMessage` extend or wrap `ChatMessage`?** Current proposal: independent hierarchy with conversion. This keeps the agent type system clean and prevents Core dependencies from leaking in. **Recommendation: Independent + conversion.**

2. **Should `IAgentTool` extend `ITool`?** The existing `ITool` returns `string`; `IAgentTool` returns `AgentToolResult` (text/image + details). **Recommendation: Independent interface. Provide an adapter `ToolAdapter : IAgentTool` that wraps any `ITool`.**

3. **Thinking budget support?** pi-mono has `thinkingBudgets` mapping `ThinkingLevel` → token count. Should this be part of Sprint 1 types or deferred? **Recommendation: Include in types (Sprint 1), implement in loop (Sprint 2).**

4. **Naming: `AgentLoopRunner` vs `AgentLoop`?** The existing `BotNexus.Agent.AgentLoop` makes `AgentLoop` confusing even though they're in different namespaces. **Recommendation: `AgentLoopRunner` to avoid confusion.**

---

## Type Reference (Complete Mapping)

| pi-mono Type | C# Type | File | Sprint |
|-------------|---------|------|--------|
| `AgentMessage` | `AgentMessage` (abstract record) | `AgentMessage.cs` | 1 |
| `UserMessage` | `UserMessage : AgentMessage` | `AgentMessage.cs` | 1 |
| `AssistantMessage` | `AssistantMessage : AgentMessage` | `AgentMessage.cs` | 1 |
| `ToolResultMessage` | `ToolResultMessage : AgentMessage` | `AgentMessage.cs` | 1 |
| `AgentEvent` | `AgentEvent` (abstract record) | `AgentEvent.cs` | 1 |
| 10 event types | 10 record subtypes | `AgentEvent.cs` | 1 |
| `AgentEventType` | `AgentEventType` (enum) | `AgentEventType.cs` | 1 |
| `AgentState` | `AgentState` (class) | `AgentState.cs` | 1 |
| `AgentTool` | `IAgentTool` (interface) | `IAgentTool.cs` | 1 |
| `AgentToolResult` | `AgentToolResult` (record) | `AgentToolResult.cs` | 1 |
| `AgentContext` | `AgentContext` (record) | `AgentContext.cs` | 1 |
| `AgentLoopConfig` | `AgentLoopConfig` (record) | `AgentLoopConfig.cs` | 1 |
| `AgentOptions` | `AgentOptions` (record) | `AgentOptions.cs` | 1 |
| `ThinkingLevel` | `ThinkingLevel` (enum) | `ThinkingLevel.cs` | 1 |
| `ToolExecutionMode` | `ToolExecutionMode` (enum) | `ToolExecutionMode.cs` | 1 |
| `StreamFn` | `ILlmProvider.ChatStreamAsync` | (reuse) | — |
| `BeforeToolCallContext` | `BeforeToolCallContext` (record) | `BeforeToolCallContext.cs` | 1 |
| `AfterToolCallContext` | `AfterToolCallContext` (record) | `AfterToolCallContext.cs` | 1 |
| `BeforeToolCallResult` | `BeforeToolCallResult` (record) | `BeforeToolCallResult.cs` | 1 |
| `AfterToolCallResult` | `AfterToolCallResult` (record) | `AfterToolCallResult.cs` | 1 |
| `EventStream` | `ChannelReader<AgentEvent>` | (framework) | — |
| `runLoop` | `AgentLoopRunner.RunAsync` | `AgentLoopRunner.cs` | 2 |
| `streamAssistantResponse` | `StreamAccumulator.AccumulateAsync` | `StreamAccumulator.cs` | 2 |
| `executeToolCalls` | `ToolExecutor.ExecuteAsync` | `ToolExecutor.cs` | 2 |
| `Agent` class | `Agent` class | `Agent.cs` | 3 |
| `PendingMessageQueue` | `PendingMessageQueue` | `PendingMessageQueue.cs` | 3 |

---

## File Layout (Final State)

```
src/BotNexus.AgentCore/
├── BotNexus.AgentCore.csproj
├── README.md
├── Types/
│   ├── AgentMessage.cs
│   ├── AgentEvent.cs
│   ├── AgentEventType.cs
│   ├── AgentState.cs
│   ├── AgentStatus.cs
│   ├── AgentContext.cs
│   ├── AgentToolResult.cs
│   ├── AgentToolContent.cs
│   ├── ThinkingLevel.cs
│   └── ToolExecutionMode.cs
├── Tools/
│   ├── IAgentTool.cs
│   ├── ToolAdapter.cs           (wraps ITool → IAgentTool)
│   └── ToolExecutor.cs
├── Hooks/
│   ├── BeforeToolCallContext.cs
│   ├── BeforeToolCallResult.cs
│   ├── AfterToolCallContext.cs
│   └── AfterToolCallResult.cs
├── Configuration/
│   ├── AgentLoopConfig.cs
│   ├── AgentOptions.cs
│   └── Delegates.cs
├── Loop/
│   ├── AgentLoopRunner.cs
│   ├── StreamAccumulator.cs
│   ├── ContextConverter.cs
│   └── MessageConverter.cs
├── Agent.cs
└── PendingMessageQueue.cs

tests/BotNexus.AgentCore.Tests/
├── BotNexus.AgentCore.Tests.csproj
├── TestUtils/
│   ├── CalculateTool.cs
│   ├── GetCurrentTimeTool.cs
│   ├── MockLlmProvider.cs
│   └── TestHelpers.cs
├── AgentLoopRunnerTests.cs
├── AgentTests.cs
└── AgentCoreE2ETests.cs
```

---

## Team Assignment Summary

| Sprint | Owner | Reviewer | Duration |
|--------|-------|----------|----------|
| Sprint 1: Foundation | Farnsworth (Platform Dev) | Leela (gate review) | 1-2 days |
| Sprint 2: Agent Loop | Bender (Runtime Dev) | Leela (gate review) | 2-3 days |
| Sprint 3: Agent Class | Bender (Runtime Dev) | Leela (gate review) | 1-2 days |
| Sprint 4: Tests + Docs | Hermes (Tester) + Kif (Documentation) | Leela (final review) | 2-3 days |

**Total estimated duration: 6-10 days**

---

## Success Criteria

1. ✅ `BotNexus.AgentCore` builds with zero warnings
2. ✅ Full solution builds with zero warnings
3. ✅ All existing tests continue to pass (no regressions)
4. ✅ Agent loop unit tests cover: text response, single tool call, multi-turn, parallel execution, cancellation, hooks, steering, follow-up, error handling
5. ✅ Agent class tests cover: prompt lifecycle, subscribe/unsubscribe, abort, wait, reset, concurrent rejection
6. ✅ E2E tests demonstrate full prompt → tool → response cycle
7. ✅ README provides clear usage guide
8. ✅ Zero references to/from `BotNexus.Agent` — complete isolation
9. ✅ All public types have XML documentation
