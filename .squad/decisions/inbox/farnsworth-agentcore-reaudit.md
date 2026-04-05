# Deep Functional Re-Audit: BotNexus.AgentCore vs pi-mono @mariozechner/pi-agent-core

**Author:** Farnsworth (Platform Dev)  
**Requested by:** Jon Bullen (Copilot)  
**Date:** 2025-07-18  
**Focus:** Runtime-critical gaps only

---

## 1. Agent Loop → LLM Call Path

**Trace:** `AgentLoopRunner.RunAsync` → appends prompts → `RunLoopAsync` → drains steering → `TransformContext` → `ContextConverter.ToProviderContext` → `BuildStreamOptionsAsync` (resolves API key) → `LlmClient.StreamSimple` → `StreamAccumulator.AccumulateAsync` → returns `AssistantAgentMessage`.

**pi-mono trace:** `runAgentLoop` → appends prompts → `runLoop` → drains steering → `transformContext` → `convertToLlm` → builds `Context` → `streamSimple` → `streamAssistantResponse` processes events → returns `AssistantMessage`.

### 🟡 DEGRADED — ThinkingLevel not mapped from AgentState to GenerationSettings

**pi-mono** maps `state.thinkingLevel` → `config.reasoning` every run:
```typescript
reasoning: this._state.thinkingLevel === "off" ? undefined : this._state.thinkingLevel,
```

**Our code:** `Agent.BuildLoopConfig()` clones `_options.GenerationSettings` (including its `Reasoning` field) but **never reads `_state.ThinkingLevel`**. If a consumer sets `agent.State.ThinkingLevel = ThinkingLevel.High` at runtime, the change is silently ignored. The `Reasoning` value stays whatever was in the original `GenerationSettings`.

**Fix needed:** In `Agent.BuildLoopConfig()`, after cloning `generationSettings`, map `_state.ThinkingLevel` to `generationSettings.Reasoning`:
```csharp
lock (_stateLock)
{
    model = _state.Model;
    if (_state.ThinkingLevel is { } level)
        generationSettings.Reasoning = level;
}
```

### 🟢 COSMETIC — `firstTurn` steering gate absent

**pi-mono** has a `skipInitialSteeringPoll` flag in `createLoopConfig` to prevent double-polling when continuing from drained steering messages. **Our code** doesn't have this optimization — `ContinueAsync` always polls steering on the first iteration. This is harmless because the queue was already drained by `DrainQueuedMessages()`, so the poll returns empty. No functional impact.

---

## 2. StreamAccumulator vs pi-mono's streamAssistantResponse

### 🟡 DEGRADED — Missing text_start, text_end, thinking_start/delta/end events

**pi-mono** handles all streaming events in a single `switch`:
- `start`, `text_start`, `text_delta`, `text_end`, `thinking_start`, `thinking_delta`, `thinking_end`, `toolcall_start`, `toolcall_delta`, `toolcall_end`, `done`, `error`

**Our `StreamAccumulator`** handles:
- ✅ `StartEvent`, `TextDeltaEvent`, `ToolCallStartEvent`, `ToolCallDeltaEvent`, `ToolCallEndEvent`, `DoneEvent`, `ErrorEvent`
- ❌ **Missing:** `TextStartEvent`, `TextEndEvent`, `ThinkingStartEvent`, `ThinkingDeltaEvent`, `ThinkingEndEvent`

All missing events exist as C# types in `AssistantMessageEvent.cs` — the provider layer emits them, but `StreamAccumulator` silently drops them.

**Runtime impact:** For `TextStartEvent`/`TextEndEvent` — minimal, since `TextDeltaEvent` carries the accumulated partial. For `ThinkingStartEvent`/`ThinkingDeltaEvent`/`ThinkingEndEvent` — **no thinking content is ever surfaced to subscribers**. Models like Claude Sonnet 4 with extended thinking emit thinking deltas, and consumers subscribed to `MessageUpdateEvent` will never see them. Additionally, the `Partial` message on these events carries the latest snapshot, and without processing them the accumulator may have a stale `current` snapshot between text deltas and the next event.

**Fix needed:** Add switch cases for all five missing events. They should follow the same pattern as `TextDeltaEvent`:
```csharp
case TextStartEvent textStart:
    current = MessageConverter.ToAgentMessage(textStart.Partial);
    await emit(new MessageUpdateEvent(
        Message: current,
        ContentDelta: null, ToolCallId: null, ToolName: null,
        ArgumentsDelta: null, FinishReason: null,
        InputTokens: current.Usage?.InputTokens,
        OutputTokens: current.Usage?.OutputTokens,
        Timestamp: DateTimeOffset.UtcNow)).ConfigureAwait(false);
    break;

case TextEndEvent textEnd:
    current = MessageConverter.ToAgentMessage(textEnd.Partial);
    await emit(new MessageUpdateEvent(
        Message: current,
        ContentDelta: null, ToolCallId: null, ToolName: null,
        ArgumentsDelta: null, FinishReason: null,
        InputTokens: current.Usage?.InputTokens,
        OutputTokens: current.Usage?.OutputTokens,
        Timestamp: DateTimeOffset.UtcNow)).ConfigureAwait(false);
    break;

case ThinkingStartEvent thinkingStart:
    current = MessageConverter.ToAgentMessage(thinkingStart.Partial);
    await emit(new MessageUpdateEvent(
        Message: current,
        ContentDelta: null, ToolCallId: null, ToolName: null,
        ArgumentsDelta: null, FinishReason: null,
        InputTokens: current.Usage?.InputTokens,
        OutputTokens: current.Usage?.OutputTokens,
        Timestamp: DateTimeOffset.UtcNow)).ConfigureAwait(false);
    break;

case ThinkingDeltaEvent thinkingDelta:
    current = MessageConverter.ToAgentMessage(thinkingDelta.Partial);
    await emit(new MessageUpdateEvent(
        Message: current,
        ContentDelta: thinkingDelta.Delta, ToolCallId: null, ToolName: null,
        ArgumentsDelta: null, FinishReason: null,
        InputTokens: current.Usage?.InputTokens,
        OutputTokens: current.Usage?.OutputTokens,
        Timestamp: DateTimeOffset.UtcNow)).ConfigureAwait(false);
    break;

case ThinkingEndEvent thinkingEnd:
    current = MessageConverter.ToAgentMessage(thinkingEnd.Partial);
    await emit(new MessageUpdateEvent(
        Message: current,
        ContentDelta: null, ToolCallId: null, ToolName: null,
        ArgumentsDelta: null, FinishReason: null,
        InputTokens: current.Usage?.InputTokens,
        OutputTokens: current.Usage?.OutputTokens,
        Timestamp: DateTimeOffset.UtcNow)).ConfigureAwait(false);
    break;
```

---

## 3. Tool Execution Flow

### 🟢 OK — Tool lookup, execution, error handling all match

Our `ToolExecutor.ExecuteToolCallCoreAsync` follows the same phases as pi-mono:
1. Find tool by name → error if missing ✅
2. `PrepareArgumentsAsync` (analogous to `prepareToolCallArguments` + `validateToolArguments`) → catch + error result ✅
3. `BeforeToolCall` hook → block support ✅
4. `ExecuteAsync` with `onUpdate` callback → catch + error result ✅
5. `AfterToolCall` hook → override content/details/isError ✅
6. Emit `ToolExecutionEndEvent` ✅

### 🟡 DEGRADED — Tool results missing message_start/message_end emission

**pi-mono's** `emitToolCallOutcome` emits three events per tool result:
```typescript
emit({ type: "tool_execution_end", ... });
emit({ type: "message_start", message: toolResultMessage });
emit({ type: "message_end", message: toolResultMessage });
```

**Our `ToolExecutor`** only emits `ToolExecutionEndEvent`. It does **not** emit `MessageStartEvent`/`MessageEndEvent` for tool result messages.

**Runtime impact:** In pi-mono, the `Agent.processEvents` handler uses `message_end` to push messages into `state.messages`. In our code, tool results are added to the loop's local `messages` list directly by `AgentLoopRunner` (lines 199-203). The `Agent.HandleEventAsync`/`ProcessEvent` handler only pushes via `MessageEndEvent`. This means **`Agent.State.Messages` does NOT get tool result messages appended during the run** — only the final message from `AgentEndEvent` is captured by the loop runner's return value, but the state's live messages list is missing tool results until the run completes and the caller inspects the return value.

**Fix needed:** After `ToolExecutor` returns results, `AgentLoopRunner` should emit `MessageStartEvent`/`MessageEndEvent` for each `ToolResultAgentMessage` — or `Agent.ProcessEvent` should handle `ToolExecutionEndEvent` to append tool results to state. The cleanest fix is to have `ToolExecutor` (or `AgentLoopRunner`) emit the message lifecycle events as pi-mono does.

### 🟢 OK — Parallel vs sequential execution

Both implementations support the same two modes. Our parallel mode correctly emits all `ToolExecutionStartEvent` before execution, runs concurrently via `Task.WhenAll`, and emits `ToolExecutionEndEvent` in original order.

### 🟢 OK — prepareArguments / PrepareArgumentsAsync

pi-mono calls `tool.prepareArguments` synchronously then `validateToolArguments` (AJV schema validation). Our code calls `tool.PrepareArgumentsAsync` (async, combining both steps). Functionally equivalent — the tool implementation is responsible for its own validation.

---

## 4. Context Conversion

### 🟢 OK — ContextConverter.ToProviderContext

Correctly maps:
- `agentContext.SystemPrompt` → `Context.SystemPrompt` ✅
- `agentContext.Messages` → delegate to `convertToLlm` → `Context.Messages` ✅
- `agentContext.Tools` → `Tool[]` via `ToProviderTool` (maps Name, Description, Parameters) ✅

Matches pi-mono's context construction in `streamAssistantResponse` (lines 255-259).

---

## 5. MessageConverter

### 🟢 OK — UserMessage conversion

Text-only and multimodal (text + images) both handled correctly. Data URI parsing matches pi-mono's image block format.

### 🟢 OK — AssistantAgentMessage ↔ provider AssistantMessage

`ToAgentMessage`: Joins TextContent, extracts ToolCallContent, maps Usage, StopReason, ErrorMessage. ✅  
`ToProviderAssistantMessage`: Reconstructs content blocks (TextContent + ToolCallContent), maps usage. ✅

### 🟢 OK — ToolResultAgentMessage conversion

Maps `ToolCallId`, `ToolName`, `Content` (text/image blocks), `IsError`, `Timestamp`. ✅

### 🟢 COSMETIC — SystemAgentMessage silently dropped

`MessageConverter.ToProviderMessages` has no `case SystemAgentMessage:` — system messages in the timeline are silently dropped during conversion. This matches pi-mono's `defaultConvertToLlm` which only passes through `user`, `assistant`, and `toolResult` roles. System prompt is handled separately via `Context.SystemPrompt`. No functional gap.

---

## 6. Agent Class Lifecycle

### 🟢 OK — prompt → run → collect flow

`Agent.PromptAsync` → `RunAsync` → acquires `_runLock` → sets `Running` status → creates linked CTS → calls `AgentLoopRunner.RunAsync` → `HandleEventAsync` dispatches to `ProcessEvent` + listeners → returns new messages → cleans up.

Matches pi-mono's `Agent.prompt` → `runWithLifecycle` → `runAgentLoop` → `processEvents` + listeners → `finishRun`.

### 🟢 OK — State updates from events

Our `ProcessEvent` correctly handles:
- `MessageStartEvent` → sets `StreamingMessage` ✅
- `MessageUpdateEvent` → updates `StreamingMessage` ✅
- `MessageEndEvent` → clears `StreamingMessage`, appends to `Messages` ✅
- `ToolExecutionStartEvent` → adds to `PendingToolCalls` ✅
- `ToolExecutionEndEvent` → removes from `PendingToolCalls` ✅
- `TurnEndEvent` → captures `ErrorMessage` ✅
- `AgentEndEvent` → clears `StreamingMessage` ✅

---

## 7. GetApiKey Delegate Invocation

### 🟢 OK — API key resolved before each LLM request

**Our code:** `AgentLoopRunner.BuildStreamOptionsAsync` calls `config.GetApiKey(config.Model.Provider, cancellationToken)` and sets `options.ApiKey` before every `LlmClient.StreamSimple` call (lines 225-237). This is called inside the inner loop, so it runs before every LLM invocation.

**pi-mono:** `streamAssistantResponse` calls `config.getApiKey(config.model.provider)` before each `streamFunction` call (lines 264-265).

Both resolve the key fresh for each turn, supporting expiring OAuth tokens. ✅

---

## Summary of Findings

| # | Area | Severity | Issue |
|---|------|----------|-------|
| 1 | StreamAccumulator | 🟡 DEGRADED | Missing `TextStartEvent`, `TextEndEvent`, `ThinkingStartEvent`, `ThinkingDeltaEvent`, `ThinkingEndEvent` — thinking content never surfaced |
| 2 | ToolExecutor/Loop | 🟡 DEGRADED | Tool result messages not emitted as `MessageStartEvent`/`MessageEndEvent` — `Agent.State.Messages` incomplete during runs |
| 3 | Agent.BuildLoopConfig | 🟡 DEGRADED | `State.ThinkingLevel` changes ignored — never mapped to `GenerationSettings.Reasoning` |
| 4 | MessageConverter | 🟢 COSMETIC | `SystemAgentMessage` silently dropped (matches pi-mono behavior, not a bug) |

No 🔴 BLOCKERs found. The three 🟡 DEGRADED issues will cause observable runtime differences:
- Extended thinking models produce no thinking-stream events for subscribers.
- Mid-run state observers will see incomplete tool results in `Agent.State.Messages`.
- Runtime `ThinkingLevel` changes via `agent.State.ThinkingLevel` have no effect.
