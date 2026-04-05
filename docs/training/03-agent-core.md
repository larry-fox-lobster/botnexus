# 03 — The Agent Core

The agent core is the engine. It drives the loop of sending context to an LLM, parsing the response, executing tools, and repeating until the model is done.

## Agent Class: State and Lifecycle

The `Agent` class is the stateful wrapper around the loop. It owns the conversation, manages concurrency, and emits lifecycle events.

```csharp
// BotNexus.AgentCore
public sealed class Agent
{
    // Mutable runtime state
    public AgentState State { get; }

    // Current execution status
    public AgentStatus Status { get; }  // Idle, Running, or Aborting
}
```

### Creating an Agent

```csharp
var agent = new Agent(new AgentOptions(
    InitialState: new AgentInitialState(
        SystemPrompt: "You are a helpful assistant.",
        Model: model,
        Tools: new IAgentTool[] { readTool, writeTool }
    ),
    Model: model,
    LlmClient: llmClient,
    ConvertToLlm: MessageConverter.ToProviderMessages,
    TransformContext: (ctx, ct) => ValueTask.FromResult(ctx),
    GetApiKey: (provider, ct) => ValueTask.FromResult(apiKey),
    GetSteeringMessages: null,
    GetFollowUpMessages: null,
    ToolExecutionMode: ToolExecutionMode.Sequential,
    BeforeToolCall: null,
    AfterToolCall: null,
    GenerationSettings: new SimpleStreamOptions
    {
        Reasoning = ThinkingLevel.Medium,
        CacheRetention = CacheRetention.Short
    },
    SteeringMode: QueueMode.All,
    FollowUpMode: QueueMode.OneAtATime
));
```

### Lifecycle Methods

#### Subscribe — Listen for events

```csharp
// Register a listener for all lifecycle events
IDisposable subscription = agent.Subscribe(async (evt, ct) =>
{
    switch (evt)
    {
        case MessageUpdateEvent update when update.ContentDelta is not null:
            Console.Write(update.ContentDelta);  // Stream text to console
            break;

        case ToolExecutionStartEvent toolStart:
            Console.WriteLine($"[Running {toolStart.ToolName}...]");
            break;

        case AgentEndEvent end:
            Console.WriteLine($"\n[Done — {end.Messages.Count} messages]");
            break;
    }
});

// Unsubscribe when done
subscription.Dispose();
```

#### PromptAsync — Start a run

```csharp
// Simple text prompt
IReadOnlyList<AgentMessage> result = await agent.PromptAsync("Read the README.md file");

// Multi-message prompt
var messages = new AgentMessage[]
{
    new UserMessage("Read these files:"),
    new UserMessage("src/main.cs"),
};
result = await agent.PromptAsync(messages);
```

`PromptAsync` blocks until the agent finishes all tool calls and returns the full set of messages produced during the run.

#### ContinueAsync — Resume from current state

```csharp
// Continue without adding a new message
// Useful for retries when context already has tool results
result = await agent.ContinueAsync();
```

> **Key Takeaway:** `PromptAsync` starts a new run with new messages. `ContinueAsync` resumes from wherever the conversation left off. Both run the same loop.

### Steering and Follow-Up Queues

The agent has two message queues for injecting messages at different points:

```csharp
// Steering: injected at the next turn boundary (mid-run)
agent.Steer(new UserMessage("Actually, focus on the tests first."));

// Follow-up: injected after the current run finishes
agent.FollowUp(new UserMessage("Now commit the changes."));

// Clear queues
agent.ClearSteeringQueue();
agent.ClearFollowUpQueue();
agent.ClearAllQueues();
```

**Steering messages** are drained at each turn boundary — between tool execution and the next LLM call. Use them for course corrections.

**Follow-up messages** are drained after the agent finishes its current run with no pending tool calls. Use them for chained workflows.

Both queues support configurable drain modes:
- `QueueMode.All` — Drain all messages at once
- `QueueMode.OneAtATime` — Drain one message per boundary

### Abort and Reset

```csharp
// Abort the current run (waits for clean shutdown)
await agent.AbortAsync();

// Wait for idle (useful when coordinating from another thread)
await agent.WaitForIdleAsync();

// Full reset: clears history, queues, and state
agent.Reset();
```

## AgentLoopRunner: The Inner/Outer Loop

`AgentLoopRunner` is a static class that implements the actual loop. The `Agent` class delegates to it.

### The Loop, Visualized

```
PromptAsync("Fix the bug")
│
├─ Emit: AgentStartEvent
├─ Emit: TurnStartEvent
│
│  ┌─── TURN LOOP ──────────────────────────────────────────────────┐
│  │                                                                 │
│  │  1. Drain steering messages → add to timeline                   │
│  │  2. Transform context (e.g., compaction)                        │
│  │  3. Convert agent messages → provider messages                  │
│  │  4. Call LLM via StreamSimple()                                 │
│  │  5. Accumulate stream → AssistantAgentMessage                   │
│  │  6. Add assistant message to timeline                           │
│  │                                                                 │
│  │  If StopReason is Error/Aborted/Refusal/Sensitive:              │
│  │    → Emit TurnEndEvent, AgentEndEvent, STOP                     │
│  │                                                                 │
│  │  If assistant has ToolCalls:                                     │
│  │    7. Execute tools (sequential or parallel)                    │
│  │    8. Add tool results to timeline                              │
│  │    9. Emit TurnEndEvent                                         │
│  │   10. Drain steering messages                                   │
│  │   11. → Back to step 1 (new turn)                               │
│  │                                                                 │
│  │  If no ToolCalls:                                               │
│  │    → Emit TurnEndEvent                                          │
│  │    → Check follow-up queue                                      │
│  │                                                                 │
│  └─────────────────────────────────────────────────────────────────┘
│
│  ┌─── FOLLOW-UP LOOP ────────────────────┐
│  │  If follow-up messages exist:          │
│  │    → Drain them, add to timeline       │
│  │    → Back to TURN LOOP                 │
│  │  If no follow-ups:                     │
│  │    → STOP                              │
│  └────────────────────────────────────────┘
│
├─ Emit: AgentEndEvent(all new messages)
└─ Return all new messages
```

### Implementation Highlights

```csharp
// BotNexus.AgentCore.Loop
public static class AgentLoopRunner
{
    // Start a new run
    public static Task<IReadOnlyList<AgentMessage>> RunAsync(
        IReadOnlyList<AgentMessage> prompts,
        AgentContext context,
        AgentLoopConfig config,
        Func<AgentEvent, Task> emit,
        CancellationToken ct);

    // Continue from current state
    public static Task<IReadOnlyList<AgentMessage>> ContinueAsync(
        AgentContext context,
        AgentLoopConfig config,
        Func<AgentEvent, Task> emit,
        CancellationToken ct);
}
```

The loop calls `config.TransformContext` before each LLM call, giving the caller a chance to modify the context (e.g., compaction, filtering).

## StreamAccumulator: Events → Messages

`StreamAccumulator` consumes provider streaming events and produces agent-level events.

```
Provider Events                Agent Events
─────────────                  ────────────
StartEvent         ──────────▶ MessageStartEvent
TextDeltaEvent     ──────────▶ MessageUpdateEvent(ContentDelta=...)
ThinkingDeltaEvent ──────────▶ MessageUpdateEvent(IsThinking=true)
ToolCallStartEvent ──────────▶ MessageUpdateEvent(ToolCallId=..., ToolName=...)
ToolCallDeltaEvent ──────────▶ MessageUpdateEvent(ArgumentsDelta=...)
DoneEvent          ──────────▶ MessageEndEvent
ErrorEvent         ──────────▶ MessageEndEvent(FinishReason=Error)
```

The accumulator maintains a running `AssistantAgentMessage` and updates it with each event. Every `MessageUpdateEvent` includes the full accumulated message — consumers never need to reconstruct state from deltas.

```csharp
// BotNexus.AgentCore.Loop
internal static class StreamAccumulator
{
    public static async Task<AssistantAgentMessage> AccumulateAsync(
        LlmStream stream,
        Func<AgentEvent, Task> emit,
        CancellationToken ct)
    {
        await foreach (var evt in stream.WithCancellation(ct))
        {
            switch (evt)
            {
                case StartEvent start:
                    await emit(new MessageStartEvent(...));
                    break;

                case TextDeltaEvent delta:
                    // Update accumulated message, emit update
                    await emit(new MessageUpdateEvent(
                        Message: accumulatedMessage,
                        ContentDelta: delta.Delta,
                        IsThinking: false, ...));
                    break;

                case DoneEvent done:
                    await emit(new MessageEndEvent(finalMessage, ...));
                    return ConvertToAgentMessage(done.Message);

                case ErrorEvent error:
                    await emit(new MessageEndEvent(errorMessage, ...));
                    return ConvertToAgentMessage(error.Error);
            }
        }
    }
}
```

## ToolExecutor: Running Tools with Hooks

`ToolExecutor` handles the full tool execution lifecycle: lookup → validate → before-hook → execute → after-hook → emit.

### Sequential Execution

```
For each tool call in assistant message:
  1. Look up tool by name (case-insensitive)
  2. Call tool.PrepareArgumentsAsync(rawArgs)
  3. Call BeforeToolCall hook → allow or block
  4. If blocked: create error result, skip to step 7
  5. Emit ToolExecutionStartEvent
  6. Call tool.ExecuteAsync(toolCallId, validatedArgs)
  7. Call AfterToolCall hook → optionally transform result
  8. Emit ToolExecutionEndEvent
  9. Create ToolResultAgentMessage
```

### Parallel Execution

```
Phase 1 — Prepare (sequential):
  For each tool call:
    1. Look up tool, validate args, call BeforeToolCall hook
    2. Emit ToolExecutionStartEvent (all starts emitted upfront)

Phase 2 — Execute (concurrent):
  await Task.WhenAll(preparedTools.Select(t => t.ExecuteAsync(...)))

Phase 3 — Finalize (sequential, deterministic order):
  For each tool call (in original assistant order):
    1. Call AfterToolCall hook
    2. Emit ToolExecutionEndEvent
    3. Create ToolResultAgentMessage
```

> **Key Takeaway:** Parallel mode prepares tools sequentially (for deterministic hook ordering), executes them concurrently, and emits results in the original order. Events are always deterministic regardless of execution timing.

### Hook Orchestration

```csharp
// Before hook: validate and optionally block
BeforeToolCallDelegate? beforeToolCall = async (context, ct) =>
{
    if (context.ToolCallRequest.Name == "bash"
        && context.ValidatedArgs["command"]?.ToString()?.Contains("rm -rf") == true)
    {
        return new BeforeToolCallResult(Block: true, Reason: "Destructive command blocked");
    }
    return new BeforeToolCallResult(Block: false);
};

// After hook: transform results
AfterToolCallDelegate? afterToolCall = async (context, ct) =>
{
    // Redact sensitive content
    if (context.ToolName == "read" && ContainsSensitiveData(context.Result))
    {
        return new AfterToolCallResult(
            Content: new[] { new AgentToolContent(AgentToolContentType.Text, "[REDACTED]") }
        );
    }
    return null;  // No transformation
};
```

## MessageConverter: Agent ↔ Provider Messages

The agent has its own message types (simpler, tool-focused). `MessageConverter` bridges the two systems.

```csharp
internal static class MessageConverter
{
    // Agent → Provider (before LLM call)
    public static IReadOnlyList<Message> ToProviderMessages(
        IReadOnlyList<AgentMessage> agentMessages);

    // Provider → Agent (after LLM response)
    public static AssistantAgentMessage ToAgentMessage(
        AssistantMessage providerMessage);
}
```

**Agent messages are simpler:**
- `UserMessage` → text + optional images
- `AssistantAgentMessage` → text + tool calls + usage + finish reason
- `ToolResultAgentMessage` → tool result with error flag

**Provider messages are richer:**
- `UserMessage` → union content (string or ContentBlock[])
- `AssistantMessage` → ContentBlock[] with full metadata
- `ToolResultMessage` → ContentBlock[] with details

The converter handles image parsing (data URIs), text concatenation (joining multiple TextContent blocks), and usage mapping.

## Agent Events

The full event lifecycle for a single agent run:

```
AgentStartEvent
│
├─ TurnStartEvent
│  ├─ MessageStartEvent(UserMessage)          ← Your prompt
│  ├─ MessageEndEvent(UserMessage)
│  │
│  ├─ MessageStartEvent(AssistantMessage)     ← LLM starts responding
│  ├─ MessageUpdateEvent(ContentDelta="...")   ← Streaming chunks
│  ├─ MessageUpdateEvent(ContentDelta="...")
│  ├─ MessageUpdateEvent(ToolCallId="...", ToolName="read")  ← Tool call streaming
│  ├─ MessageEndEvent(AssistantMessage)       ← LLM done
│  │
│  ├─ ToolExecutionStartEvent("read")         ← Tool execution
│  ├─ ToolExecutionEndEvent("read", Result)
│  │
│  └─ TurnEndEvent(AssistantMessage, [ToolResult])
│
├─ TurnStartEvent                             ← New turn (tool result → LLM)
│  ├─ MessageStartEvent(AssistantMessage)
│  ├─ MessageUpdateEvent(ContentDelta="...")
│  ├─ MessageEndEvent(AssistantMessage)       ← No tool calls → done
│  └─ TurnEndEvent(AssistantMessage, [])
│
└─ AgentEndEvent(all new messages)
```

### Event Types Reference

| Event | When | Key Data |
|-------|------|----------|
| `AgentStartEvent` | Run begins | — |
| `AgentEndEvent` | Run completes | `Messages` — all messages from this run |
| `TurnStartEvent` | New LLM call begins | — |
| `TurnEndEvent` | LLM call + tools complete | `Message` + `ToolResults` |
| `MessageStartEvent` | Message processing begins | `Message` |
| `MessageUpdateEvent` | Streaming chunk | `ContentDelta`, `IsThinking`, `ToolCallId`, `ArgumentsDelta` |
| `MessageEndEvent` | Message complete | `Message` (final) |
| `ToolExecutionStartEvent` | Tool about to execute | `ToolCallId`, `ToolName`, `Args` |
| `ToolExecutionUpdateEvent` | Tool progress (reserved) | `PartialResult` |
| `ToolExecutionEndEvent` | Tool finished | `Result`, `IsError` |

## AgentState: Mutable Runtime State

```csharp
public class AgentState
{
    // Settable — changes take effect on next run
    public string? SystemPrompt { get; set; }
    public required LlmModel Model { get; set; }
    public ThinkingLevel? ThinkingLevel { get; set; }
    public IReadOnlyList<IAgentTool> Tools { get; set; }
    public IReadOnlyList<AgentMessage> Messages { get; set; }

    // Read-only — updated by the loop
    public bool IsStreaming { get; }
    public AssistantAgentMessage? StreamingMessage { get; }
    public IReadOnlySet<string> PendingToolCalls { get; }
    public string? ErrorMessage { get; }
}
```

You can modify `SystemPrompt`, `Model`, `Tools`, and `Messages` between runs. Changes to these properties don't affect an in-flight run — they take effect on the next `PromptAsync` or `ContinueAsync`.

## The IAgentTool Interface

Every tool the agent can invoke implements this interface:

```csharp
public interface IAgentTool
{
    // Identity
    string Name { get; }              // Unique name exposed to model (e.g., "read")
    string Label { get; }             // Human-readable label for logs
    Tool Definition { get; }          // JSON Schema definition for the model

    // Lifecycle
    Task<IReadOnlyDictionary<string, object?>> PrepareArgumentsAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);

    Task<AgentToolResult> ExecuteAsync(
        string toolCallId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default,
        AgentToolUpdateCallback? onUpdate = null);

    // Optional prompt contributions
    string? GetPromptSnippet();
    IReadOnlyList<string> GetPromptGuidelines();
}
```

**`PrepareArgumentsAsync`** — Validate and normalize arguments before execution. Throw to reject. Called sequentially even in parallel mode.

**`ExecuteAsync`** — Do the work. Return an `AgentToolResult` with text or image content. Must be thread-safe for parallel execution.

**`GetPromptSnippet`** — One-line description for the system prompt's tool listing.

**`GetPromptGuidelines`** — Additional instructions contributed to the system prompt.

### Tool Result

```csharp
public record AgentToolResult(
    IReadOnlyList<AgentToolContent> Content,  // Text or image blocks
    object? Details = null                     // Metadata (not sent to LLM)
);

public record AgentToolContent(
    AgentToolContentType Type,  // Text or Image
    string Value                // Content string
);
```

## Configuration Records

### AgentOptions

The complete configuration for creating an `Agent`:

```csharp
public record AgentOptions(
    AgentInitialState? InitialState,       // Seed state (prompt, model, tools, messages)
    LlmModel Model,                        // Default model
    LlmClient LlmClient,                  // Provider client
    ConvertToLlmDelegate ConvertToLlm,    // Agent → provider message conversion
    TransformContextDelegate TransformContext,  // Context transformation before LLM
    GetApiKeyDelegate GetApiKey,           // API key resolution
    GetMessagesDelegate? GetSteeringMessages,
    GetMessagesDelegate? GetFollowUpMessages,
    ToolExecutionMode ToolExecutionMode,   // Sequential or Parallel
    BeforeToolCallDelegate? BeforeToolCall,
    AfterToolCallDelegate? AfterToolCall,
    SimpleStreamOptions GenerationSettings,
    QueueMode SteeringMode,
    QueueMode FollowUpMode,
    string? SessionId = null
);
```

### Delegate Signatures

```csharp
// Convert agent messages to provider format
delegate IReadOnlyList<Message> ConvertToLlmDelegate(IReadOnlyList<AgentMessage> messages);

// Transform context before each LLM call
delegate ValueTask<AgentContext> TransformContextDelegate(
    AgentContext context, CancellationToken ct);

// Resolve API key on demand
delegate ValueTask<string?> GetApiKeyDelegate(string provider, CancellationToken ct);

// Provide additional messages at queue drain points
delegate Task<IReadOnlyList<AgentMessage>> GetMessagesDelegate(CancellationToken ct);

// Pre-tool hook
delegate Task<BeforeToolCallResult?> BeforeToolCallDelegate(
    BeforeToolCallContext context, CancellationToken ct);

// Post-tool hook
delegate Task<AfterToolCallResult?> AfterToolCallDelegate(
    AfterToolCallContext context, CancellationToken ct);
```

## What's Next

- **[Coding Agent](04-coding-agent.md)** — How CodingAgent wires tools, extensions, and safety
- **[Build Your Own Agent](05-building-your-own.md)** — Hands-on tutorial
