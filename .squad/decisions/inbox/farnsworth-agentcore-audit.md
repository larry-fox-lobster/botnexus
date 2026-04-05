# AgentCore Alignment Audit

**Auditor:** Farnsworth (Platform Dev)
**Date:** 2025-07-15
**pi-mono commit:** `1a6a58eb05f7256ecf51cce6c2cae2f9e464d712`
**BotNexus path:** `src/agent/BotNexus.AgentCore/`

## Summary

**9/11 areas aligned, 2 gaps found.**

The C# port is a faithful, idiomatic translation of pi-agent-core. Core loop logic, event lifecycle, tool execution modes, hook contracts, state management, and queue semantics all match. Two meaningful gaps exist: (1) missing `toolCallId` parameter on `IAgentTool.ExecuteAsync`, and (2) missing `onUpdate` callback for streaming tool progress. A third minor difference is the absence of `proxy.ts` (streamProxy), which is reasonable since BotNexus uses server-side `LlmClient` directly.

---

## Detailed Findings

### 1. AgentMessage Types — ⚠️ Partial

**pi-mono:**
- `AgentMessage = Message | CustomAgentMessages[keyof CustomAgentMessages]` — a union of LLM messages (`UserMessage`, `AssistantMessage`, `ToolResultMessage`) plus extensible custom messages via declaration merging.
- `AssistantMessage` has: `role`, `content[]` (text/thinking/toolCall blocks), `api`, `provider`, `model`, `usage` (with full cost breakdown: input/output/cacheRead/cacheWrite/totalTokens/cost), `stopReason`, `errorMessage`, `timestamp`, optionally `responseId`.
- `UserMessage` has: `role`, `content[]` (text + image blocks), `timestamp`.
- `ToolResultMessage` has: `role`, `toolCallId`, `toolName`, `content[]`, `details`, `isError`, `timestamp`.
- Custom messages are extensible via TypeScript declaration merging on `CustomAgentMessages` interface.

**BotNexus:**
- `AgentMessage(string Role)` — abstract record base. Subtypes: `UserMessage`, `AssistantAgentMessage`, `ToolResultAgentMessage`, `SystemAgentMessage`.
- `AssistantAgentMessage` has: `Content` (single string), `ToolCalls`, `FinishReason`, `Usage` (InputTokens/OutputTokens only), `ErrorMessage`, `Timestamp`.
- `UserMessage` has: `Content` (single string), `Images` (list of `AgentImageContent`).
- `ToolResultAgentMessage` has: `ToolCallId`, `ToolName`, `Result` (AgentToolResult), `IsError`, `Timestamp`.
- `SystemAgentMessage` exists (no pi-mono equivalent — pi-mono uses `systemPrompt` in context, not a system message type).

**Gap:**
1. **Usage granularity**: pi-mono has `cacheRead`, `cacheWrite`, `totalTokens`, and full `cost` breakdown. BotNexus only has `InputTokens` and `OutputTokens`. Missing cache and cost metrics.
2. **Content model flattening**: pi-mono `AssistantMessage.content` is a heterogeneous array (`TextContent | ThinkingContent | ToolCallContent`). BotNexus flattens text into a single `string Content` and separates tool calls into `ToolCalls`. Thinking content is lost during the conversion.
3. **Custom message extensibility**: pi-mono uses declaration merging for custom message types. BotNexus uses the abstract record pattern, which is extensible via subclassing — functionally equivalent.
4. **SystemAgentMessage**: BotNexus has an explicit `SystemAgentMessage` subtype. pi-mono uses `systemPrompt` on `AgentContext` instead. Minor structural difference, not a functional gap.

**Action:** Consider adding `CacheReadTokens`, `CacheWriteTokens`, `TotalTokens` to `AgentUsage`. Thinking content loss is acceptable for now since it's not sent back to the LLM.

---

### 2. AgentEvent Types — ✅ Aligned

**pi-mono** (10 event types):
| Event | Fields |
|---|---|
| `agent_start` | (none) |
| `agent_end` | `messages: AgentMessage[]` |
| `turn_start` | (none) |
| `turn_end` | `message: AgentMessage`, `toolResults: ToolResultMessage[]` |
| `message_start` | `message: AgentMessage` |
| `message_update` | `message: AgentMessage`, `assistantMessageEvent: AssistantMessageEvent` |
| `message_end` | `message: AgentMessage` |
| `tool_execution_start` | `toolCallId`, `toolName`, `args` |
| `tool_execution_update` | `toolCallId`, `toolName`, `args`, `partialResult` |
| `tool_execution_end` | `toolCallId`, `toolName`, `result`, `isError` |

**BotNexus** (10 event types via `AgentEventType` enum):
| Event | Fields |
|---|---|
| `AgentStartEvent` | `Timestamp` |
| `AgentEndEvent` | `Messages`, `Timestamp` |
| `TurnStartEvent` | `Timestamp` |
| `TurnEndEvent` | `Message` (AssistantAgentMessage), `ToolResults`, `Timestamp` |
| `MessageStartEvent` | `Message`, `Timestamp` |
| `MessageUpdateEvent` | `Message`, `ContentDelta`, `ToolCallId`, `ToolName`, `ArgumentsDelta`, `FinishReason`, `InputTokens`, `OutputTokens`, `Timestamp` |
| `MessageEndEvent` | `Message`, `Timestamp` |
| `ToolExecutionStartEvent` | `ToolCallId`, `ToolName`, `Args`, `Timestamp` |
| `ToolExecutionUpdateEvent` | `ToolCallId`, `ToolName`, `Args`, `PartialResult`, `Timestamp` |
| `ToolExecutionEndEvent` | `ToolCallId`, `ToolName`, `Result`, `IsError`, `Timestamp` |

**Gap:**
- pi-mono `message_start`/`message_end` accept any `AgentMessage`. BotNexus `MessageStartEvent`/`MessageEndEvent` use `AssistantAgentMessage`. This means non-assistant messages emitted by the loop (user prompts, tool results) need to be wrapped in `ToDisplayMessage()`, which is a lossy conversion. This is a design choice, not a bug — the C# code wraps them to emit events.
- pi-mono's `message_update` passes the raw `AssistantMessageEvent` from the stream. BotNexus decomposes it into explicit fields (`ContentDelta`, `ToolCallId`, `ToolName`, `ArgumentsDelta`, `FinishReason`, token counts). This is actually **richer** than pi-mono for consumers.
- BotNexus adds `Timestamp` to every event. pi-mono events have no timestamp. Minor enhancement.

**Action:** No action required. All 10 events present with semantically equivalent payloads.

---

### 3. AgentState — ✅ Aligned

**pi-mono:**
- `systemPrompt: string`
- `model: Model<any>`
- `thinkingLevel: ThinkingLevel` (default `"off"`)
- `tools` — getter/setter with array copy
- `messages` — getter/setter with array copy
- `readonly isStreaming: boolean`
- `readonly streamingMessage?: AgentMessage`
- `readonly pendingToolCalls: ReadonlySet<string>`
- `readonly errorMessage?: string`

**BotNexus:**
- `SystemPrompt: string?`
- `Model: LlmModel` (required)
- `ThinkingLevel: ThinkingLevel?` (default `null`)
- `Tools` — getter/setter with list copy ✅
- `Messages` — getter/setter with list copy ✅
- `IsStreaming` — computed from `StreamingMessage is not null` ✅
- `StreamingMessage: AssistantAgentMessage?` — private setter via `SetStreamingMessage()` ✅
- `PendingToolCalls: IReadOnlySet<string>` — private via `SetPendingToolCalls()` ✅
- `ErrorMessage: string?` — private setter via `SetErrorMessage()` ✅

**Gap:** pi-mono defaults `thinkingLevel` to `"off"`. BotNexus defaults to `null`. Functionally equivalent since `null` maps to "don't send reasoning param" which is effectively off.

**Action:** None. Fully aligned.

---

### 4. AgentContext — ✅ Aligned

**pi-mono:**
```typescript
interface AgentContext {
  systemPrompt: string;
  messages: AgentMessage[];
  tools?: AgentTool<any>[];
}
```

**BotNexus:**
```csharp
record AgentContext(
    string? SystemPrompt,
    IReadOnlyList<AgentMessage> Messages,
    IReadOnlyList<IAgentTool> Tools);
```

**Gap:** None. 1:1 match. Both are snapshots passed through the loop.

**Action:** None.

---

### 5. AgentLoopConfig — ✅ Aligned

**pi-mono:**
- `model`, `convertToLlm`, `transformContext?`, `getApiKey?`, `getSteeringMessages?`, `getFollowUpMessages?`, `toolExecution?`, `beforeToolCall?`, `afterToolCall?`
- Also extends `SimpleStreamOptions` (inherits `apiKey`, `temperature`, `maxTokens`, `reasoning`, `sessionId`, `onPayload`, `transport`, `thinkingBudgets`, `maxRetryDelayMs`, `signal`).

**BotNexus:**
- `Model`, `ConvertToLlm`, `TransformContext`, `GetApiKey`, `GetSteeringMessages?`, `GetFollowUpMessages?`, `ToolExecutionMode`, `BeforeToolCall?`, `AfterToolCall?`, `GenerationSettings` (contains all stream options).

**Gap:**
- pi-mono `AgentLoopConfig extends SimpleStreamOptions` (flat). BotNexus wraps them in `GenerationSettings` (composed). Same data, different structure.
- All delegate signatures match semantically (CancellationToken vs AbortSignal is the standard C#/TS difference).

**Action:** None. Structurally equivalent.

---

### 6. Agent Class — ✅ Aligned

#### Constructor/Options

**pi-mono:** `new Agent(options?: AgentOptions)` with all-optional fields, defaults for missing values.
**BotNexus:** `new Agent(AgentOptions options)` with required options record.
**Match:** ✅ Equivalent. BotNexus is stricter (required param) which is idiomatic C#.

#### prompt() / PromptAsync()

**pi-mono:** `prompt(message | messages | string, images?)` — 3 overloads in one. Throws if already running. Calls `runPromptMessages()`.
**BotNexus:** `PromptAsync(string)`, `PromptAsync(AgentMessage)`, `PromptAsync(IReadOnlyList<AgentMessage>)` — 3 overloads. Throws if already running. Calls `RunAsync()`.
**Match:** ✅ Same semantics. BotNexus returns `Task<IReadOnlyList<AgentMessage>>` (the new messages) while pi-mono returns `void` (messages are in state). This is actually an improvement — callers get the result directly.

#### continue() / ContinueAsync()

**pi-mono:** Throws if already running or last message is assistant. Drains steering → followUp queues if last is assistant. Calls `runContinuation()`.
**BotNexus:** Same behavior. Drains queued messages and calls `PromptAsync` if available. Throws `InvalidOperationException` if last message is assistant.
**Match:** ✅

#### subscribe() / Subscribe()

**pi-mono:** Returns unsubscribe function `() => void`. Listeners receive `(event, signal)`.
**BotNexus:** Returns `IDisposable`. Listeners receive `(AgentEvent, CancellationToken)`. Thread-safe via copy-on-write list.
**Match:** ✅ Idiomatic C# equivalent. Thread-safety is enhanced in BotNexus.

#### steer() / Steer() and followUp() / FollowUp()

**pi-mono:** `steer(message)` and `followUp(message)`.
**BotNexus:** `Steer(message)` and `FollowUp(message)`.
**Match:** ✅

#### abort() / AbortAsync()

**pi-mono:** `abort()` — synchronous, calls `abortController.abort()`.
**BotNexus:** `AbortAsync()` — async, cancels CTS, awaits active run settlement. Swallows `OperationCanceledException`.
**Match:** ✅ BotNexus version is more robust (awaits settlement).

#### waitForIdle() / WaitForIdleAsync()

**pi-mono:** `waitForIdle()` returns `activeRun?.promise ?? Promise.resolve()`.
**BotNexus:** `WaitForIdleAsync(ct)` — same semantics with cancellation support.
**Match:** ✅

#### reset() / Reset()

**pi-mono:** Clears messages, streaming state, pending tool calls, error, and queues.
**BotNexus:** Same behavior plus cancels CTS and sets status to Idle.
**Match:** ✅ BotNexus is more thorough.

#### State access / Status / Queues

**pi-mono:** `state` getter. `steeringMode`/`followUpMode` getters/setters. `hasQueuedMessages()`, `clearSteeringQueue()`, `clearFollowUpQueue()`, `clearAllQueues()`. `signal` getter.
**BotNexus:** `State` property. `Status` property (Idle/Running/Aborting — richer than pi-mono). Queue clearing methods match. No `Signal` getter (internal only). No `steeringMode`/`followUpMode` setters (frozen at construction).

**Gap:** pi-mono allows changing `steeringMode`/`followUpMode` at runtime. BotNexus freezes them at construction. Minor behavioral difference.

**Action:** Consider adding `SteeringMode`/`FollowUpMode` properties if runtime mode switching is needed.

---

### 7. Agent Loop — ✅ Aligned

**pi-mono `runLoop()`:**
1. Check for steering messages at start
2. Outer while(true) for follow-ups
3. Inner while (hasMoreToolCalls || pendingMessages) for turns
4. firstTurn tracking → emit turn_start
5. Inject pending messages → emit message_start/end
6. Stream assistant response
7. Check stopReason error/aborted → emit turn_end, agent_end, return
8. Execute tool calls if any
9. Append tool results to context
10. Emit turn_end
11. Poll steering messages
12. After inner loop: poll follow-ups → continue outer if any
13. Emit agent_end

**BotNexus `RunLoopAsync()`:**
1. Poll steering messages at start ✅
2. Outer while(true) for follow-ups ✅
3. Inner while (hasMoreToolCalls || pendingMessages) ✅
4. firstTurn tracking → emit TurnStartEvent ✅
5. Inject pending → emit MessageStart/End ✅
6. TransformContext → ConvertToLlm → StreamSimple → Accumulate ✅
7. Check FinishReason Error/Aborted → emit TurnEnd, AgentEnd, return ✅
8. Execute tool calls ✅
9. Append tool results ✅
10. Emit TurnEndEvent ✅
11. Poll steering ✅
12. Poll follow-ups → continue ✅
13. Emit AgentEndEvent ✅

**Gap:** None. The loop logic is a 1:1 port.

**Action:** None.

---

### 8. Tool Execution — ✅ Aligned

**pi-mono:**
- Sequential: for-each → emit start → prepare → execute → finalize (afterToolCall) → emit end
- Parallel: for-each → emit start → prepare (blocking if not). Collect runnables. Map to concurrent execution. Await in order → finalize → emit end.
- `prepareToolCall()`: find tool → prepareArguments → validateToolArguments → beforeToolCall hook → return prepared or immediate
- `executePreparedToolCall()`: call tool.execute with onUpdate callback → collect update events → return result
- `finalizeExecutedToolCall()`: call afterToolCall hook → merge overrides → emit outcome

**BotNexus:**
- Sequential: for-each → emit start → `ExecuteToolCallCoreAsync` → emit end ✅
- Parallel: for-each → emit all starts → `Task.WhenAll` → order by index → emit all ends ✅
- `ExecuteToolCallCoreAsync()`: find tool (case-insensitive) → PrepareArgumentsAsync → beforeToolCall hook → ExecuteAsync → afterToolCall hook ✅
- afterToolCall merge: `Content ?? result.Content`, `Details ?? result.Details`, `IsError ?? isError` ✅

**Gap:**
- pi-mono parallel emits `tool_execution_start` interleaved with immediate results during preparation (blocked tools get start+end before runnables). BotNexus emits ALL starts first, then runs all, then emits ALL ends. Slightly different ordering for blocked tools in parallel mode, but functionally equivalent.

**Action:** None. Behavior matches for the common case.

---

### 9. IAgentTool — ⚠️ Partial

**pi-mono `AgentTool<TParameters, TDetails>`:**
```typescript
interface AgentTool<TParameters, TDetails> extends Tool<TParameters> {
  label: string;
  prepareArguments?: (args: unknown) => Static<TParameters>;
  execute: (
    toolCallId: string,
    params: Static<TParameters>,
    signal?: AbortSignal,
    onUpdate?: AgentToolUpdateCallback<TDetails>,
  ) => Promise<AgentToolResult<TDetails>>;
}
```

**BotNexus `IAgentTool`:**
```csharp
interface IAgentTool {
  string Name { get; }
  string Label { get; }
  Tool Definition { get; }
  Task<IReadOnlyDictionary<string, object?>> PrepareArgumentsAsync(args, ct);
  Task<AgentToolResult> ExecuteAsync(args, ct);
}
```

**Gaps:**
1. **Missing `toolCallId` parameter**: pi-mono passes `toolCallId` as the first parameter to `execute()`. BotNexus `ExecuteAsync` does not receive the tool call ID. Tools that need to correlate with their call ID (e.g., for streaming updates, cancellation scoping) cannot do so.
2. **Missing `onUpdate` callback**: pi-mono passes `onUpdate?: AgentToolUpdateCallback<TDetails>` so tools can stream partial results. BotNexus has no equivalent — `ToolExecutionUpdateEvent` exists in the event schema but tools have no way to trigger it.
3. **`prepareArguments` sync vs async**: pi-mono's `prepareArguments` is synchronous. BotNexus `PrepareArgumentsAsync` is async. Not a gap — async is a superset.
4. **Generic details type**: pi-mono uses `TDetails` generic for type-safe details. BotNexus uses `object?`. Acceptable for a dynamic language port.

**Action:**
- **Add `toolCallId` parameter** to `ExecuteAsync`: `Task<AgentToolResult> ExecuteAsync(string toolCallId, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct)`.
- **Add `onUpdate` callback** to `ExecuteAsync`: `Action<AgentToolResult>? onUpdate` parameter, or a dedicated `IProgress<AgentToolResult>` parameter. Wire it through `ToolExecutor` to emit `ToolExecutionUpdateEvent`.

---

### 10. StreamFn / LlmClient.StreamSimple — ✅ Aligned

**pi-mono:**
- `StreamFn` type = `(...args: Parameters<typeof streamSimple>) => ReturnType<typeof streamSimple>`
- Agent constructor accepts optional `streamFn` (defaults to `streamSimple`).
- The loop calls `streamFn(model, context, options)` and iterates the returned `AssistantMessageEventStream`.

**BotNexus:**
- No `StreamFn` delegate. Instead, calls `LlmClient.StreamSimple(model, context, options)` directly.
- `StreamAccumulator.AccumulateAsync()` consumes the `LlmStream` and converts provider events to agent events.

**Gap:** pi-mono's `streamFn` is injectable (used by `streamProxy` for server-routed calls). BotNexus hardcodes `LlmClient.StreamSimple`. To support proxy/custom streaming, a similar delegate would be needed.

**Action:** Low priority. If proxy streaming is needed, add a `StreamDelegate` to `AgentOptions`/`AgentLoopConfig` that defaults to `LlmClient.StreamSimple`.

---

### 11. Missing Features — ❌ Two items

#### 11a. `proxy.ts` / streamProxy — ❌ Not ported

**pi-mono:** `proxy.ts` provides `streamProxy()` — a stream function that routes LLM calls through an HTTP proxy server. Handles SSE parsing, partial message reconstruction, bandwidth-optimized event stripping, and auth token injection.

**BotNexus:** No equivalent. BotNexus is server-side C# and calls providers directly, so a client-side proxy isn't needed.

**Action:** Not needed for server-side usage. If BotNexus ever needs to proxy through another service, this would need to be built. No action required now.

#### 11b. `agentLoop()` / `agentLoopContinue()` EventStream wrappers — ❌ Not ported

**pi-mono:** Exposes `agentLoop()` and `agentLoopContinue()` as public APIs that return an `EventStream<AgentEvent, AgentMessage[]>` — a push-based event stream with a terminal value. This enables consuming loop events as an async iterable without using the Agent class.

**BotNexus:** Only exposes `AgentLoopRunner.RunAsync()` and `ContinueAsync()` which use callback-based `emit` and return `Task<IReadOnlyList<AgentMessage>>`. No `IAsyncEnumerable<AgentEvent>` wrapper exists.

**Action:** Consider adding an `IAsyncEnumerable<AgentEvent>` overload for `AgentLoopRunner` if direct loop consumption (without `Agent`) is needed.

---

## Alignment Matrix

| # | Area | Status | Notes |
|---|---|---|---|
| 1 | AgentMessage types | ⚠️ Partial | Usage granularity, thinking content loss |
| 2 | AgentEvent types | ✅ Aligned | All 10 present, MessageUpdate is richer |
| 3 | AgentState | ✅ Aligned | 1:1 match with copy-on-set semantics |
| 4 | AgentContext | ✅ Aligned | Identical structure |
| 5 | AgentLoopConfig | ✅ Aligned | Composed vs flat — same data |
| 6 | Agent class | ✅ Aligned | All APIs present, C# is stricter/safer |
| 7 | Agent Loop | ✅ Aligned | 1:1 port of runLoop logic |
| 8 | Tool Execution | ✅ Aligned | Sequential + parallel modes match |
| 9 | IAgentTool | ⚠️ Partial | **Missing toolCallId and onUpdate** |
| 10 | StreamFn | ✅ Aligned | Hardcoded vs injectable — acceptable |
| 11 | Missing features | ❌ proxy.ts, EventStream API | Not needed for server-side |

## Priority Actions

1. **P1 — Add `toolCallId` to `IAgentTool.ExecuteAsync`** — Required for tool-call-scoped operations (progress, cancellation, correlation). Breaking change to interface.
2. **P1 — Add `onUpdate` callback to `IAgentTool.ExecuteAsync`** — Required to wire `ToolExecutionUpdateEvent` to actual tool progress. Currently the event type exists but is never emitted by real tools.
3. **P2 — Enrich `AgentUsage`** — Add cache token counts and cost breakdown to match pi-mono's full usage model.
4. **P3 — Consider `StreamDelegate` injection** — For future proxy/custom streaming support.
5. **P3 — Consider `IAsyncEnumerable<AgentEvent>` loop API** — For direct loop consumption without Agent wrapper.
