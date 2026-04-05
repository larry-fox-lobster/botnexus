# 02 — How Providers Work

Providers are the bridge between BotNexus and LLM APIs. This doc covers the provider interface, message types, streaming protocol, and how to add your own.

## The IApiProvider Contract

Every provider implements a single interface:

```csharp
// BotNexus.Providers.Core.Registry
public interface IApiProvider
{
    // Unique identifier for this API format (e.g., "anthropic-messages", "openai-completions")
    string Api { get; }

    // Stream a response with full control over options
    LlmStream Stream(LlmModel model, Context context, StreamOptions? options = null);

    // Stream with simplified options (reasoning level instead of raw thinking budgets)
    LlmStream StreamSimple(LlmModel model, Context context, SimpleStreamOptions? options = null);
}
```

That's it. Two streaming methods and a name. The simplicity is intentional — each provider handles its own authentication, headers, message conversion, and SSE parsing.

> **Key Takeaway:** `Stream` gives full control; `StreamSimple` adds a reasoning-level abstraction. The agent loop uses `StreamSimple` so it can specify `ThinkingLevel.High` instead of raw token budgets.

## How LlmClient Routes to Providers

`LlmClient` is the top-level entry point. It holds both registries and routes requests based on the model's `Api` field.

```csharp
// BotNexus.Providers.Core
public sealed class LlmClient
{
    public ApiProviderRegistry ApiProviders { get; }
    public ModelRegistry Models { get; }

    // Route to provider and stream
    public LlmStream Stream(LlmModel model, Context context, StreamOptions? options = null)
    {
        var provider = ResolveProvider(model.Api);  // Looks up by model.Api
        return provider.Stream(model, context, options);
    }

    // Wait for a complete response (built on top of streaming)
    public async Task<AssistantMessage> CompleteAsync(
        LlmModel model, Context context, StreamOptions? options = null)
    {
        var stream = Stream(model, context, options);
        return await stream.GetResultAsync();
    }

    private IApiProvider ResolveProvider(string api)
    {
        return ApiProviders.Get(api)
            ?? throw new InvalidOperationException($"No provider registered for API: {api}");
    }
}
```

The routing chain: `LlmModel.Api` → `ApiProviderRegistry.Get(api)` → `IApiProvider.Stream()`.

### Provider Registration

```csharp
// ApiProviderRegistry — thread-safe via ConcurrentDictionary
var registry = new ApiProviderRegistry();

// Register with optional source tracking (for batch unregister)
registry.Register(new AnthropicProvider(httpClient), sourceId: "built-in");
registry.Register(new OpenAIProvider(httpClient), sourceId: "built-in");

// Look up by API name
IApiProvider? provider = registry.Get("anthropic-messages");

// Unregister all providers from a source
registry.Unregister("built-in");
```

### Model Registration

```csharp
// ModelRegistry — hierarchical: provider → modelId → LlmModel
var models = new ModelRegistry();

models.Register("anthropic", new LlmModel(
    Id: "claude-sonnet-4",
    Name: "Claude Sonnet 4",
    Api: "anthropic-messages",       // Routes to AnthropicProvider
    Provider: "anthropic",
    BaseUrl: "https://api.anthropic.com",
    Reasoning: true,
    Input: new[] { "text", "image" },
    Cost: new ModelCost(Input: 3m, Output: 15m, CacheRead: 0.3m, CacheWrite: 3.75m),
    ContextWindow: 200_000,
    MaxTokens: 64_000
));

// Retrieve
LlmModel? model = models.GetModel("anthropic", "claude-sonnet-4");

// Cost calculation
UsageCost cost = ModelRegistry.CalculateCost(model, usage);
```

## Message Types

Messages form the conversation history. Three roles, all immutable records.

```csharp
// Base type — polymorphic by "role" discriminator
public abstract record Message(long Timestamp);

// User → LLM
public sealed record UserMessage(
    UserMessageContent Content,  // string or ContentBlock[]
    long Timestamp
) : Message(Timestamp);

// LLM → User (with metadata)
public sealed record AssistantMessage(
    IReadOnlyList<ContentBlock> Content,
    string Api,                  // Which API produced this
    string Provider,             // Which provider
    string ModelId,              // Which model
    Usage Usage,                 // Token counts and costs
    StopReason StopReason,       // Why generation stopped
    string? ErrorMessage,
    string? ResponseId,
    long Timestamp
) : Message(Timestamp);

// Tool result → LLM
public sealed record ToolResultMessage(
    string ToolCallId,           // Correlates to ToolCallContent.Id
    string ToolName,
    IReadOnlyList<ContentBlock> Content,
    bool IsError,                // Did the tool fail?
    long Timestamp,
    object? Details = null       // Metadata (not sent to LLM)
) : Message(Timestamp);
```

### Content Blocks

Each message contains `ContentBlock` items — the actual content:

```csharp
// Polymorphic by "type" discriminator
public abstract record ContentBlock;

public sealed record TextContent(
    string Text,
    string? TextSignature = null         // Provider-specific signature
) : ContentBlock;

public sealed record ThinkingContent(
    string Thinking,
    string? ThinkingSignature = null,    // For thinking continuity across turns
    bool? Redacted = null                // Redacted thinking (signature-only)
) : ContentBlock;

public sealed record ImageContent(
    string Data,                         // Base64-encoded
    string MimeType                      // e.g., "image/png"
) : ContentBlock;

public sealed record ToolCallContent(
    string Id,                           // Unique tool call ID
    string Name,                         // Tool name (matches IAgentTool.Name)
    Dictionary<string, object?> Arguments,
    string? ThoughtSignature = null      // For thought continuity
) : ContentBlock;
```

### User Message Content

`UserMessageContent` is a union type — either a plain string or rich content blocks:

```csharp
var simple = new UserMessage("What is 2 + 2?", timestamp);

var withImage = new UserMessage(
    new UserMessageContent(new ContentBlock[]
    {
        new TextContent("What's in this image?"),
        new ImageContent(base64Data, "image/png")
    }),
    timestamp
);
```

### Stop Reasons

```csharp
public enum StopReason
{
    Stop,       // Model finished naturally
    Length,     // Hit max_tokens
    ToolUse,   // Model wants to call a tool
    Error,     // Provider error
    Aborted,   // Cancelled by user
    Refusal,   // Model refused the request
    PauseTurn, // Turn paused for continuation
    Sensitive  // Content policy triggered
}
```

## Streaming Protocol

Every LLM response is a stream of events delivered through `LlmStream`.

### LlmStream

```csharp
// Channel-based async enumerable
public sealed class LlmStream : IAsyncEnumerable<AssistantMessageEvent>
{
    // Provider pushes events
    public void Push(AssistantMessageEvent evt);
    public void End(AssistantMessage? result = null);

    // Consumer iterates events
    public async IAsyncEnumerator<AssistantMessageEvent> GetAsyncEnumerator(...);

    // Or wait for the final message
    public Task<AssistantMessage> GetResultAsync();
}
```

**Usage patterns:**

```csharp
var stream = llmClient.Stream(model, context, options);

// Pattern 1: Iterate events for streaming UI
await foreach (var evt in stream)
{
    switch (evt)
    {
        case TextDeltaEvent delta:
            Console.Write(delta.Delta);   // Print each chunk as it arrives
            break;
        case DoneEvent done:
            Console.WriteLine($"\n[Tokens: {done.Message.Usage.TotalTokens}]");
            break;
    }
}

// Pattern 2: Just get the final result
AssistantMessage result = await stream.GetResultAsync();
```

### Event Hierarchy

All events carry a `Partial` field — the accumulated `AssistantMessage` so far:

```csharp
public abstract record AssistantMessageEvent(string Type);

// ── Lifecycle ────────────────────────────────────────
public sealed record StartEvent(AssistantMessage Partial)
    : AssistantMessageEvent("start");

public sealed record DoneEvent(StopReason Reason, AssistantMessage Message)
    : AssistantMessageEvent("done");

public sealed record ErrorEvent(StopReason Reason, AssistantMessage Error)
    : AssistantMessageEvent("error");

// ── Text streaming ───────────────────────────────────
public sealed record TextStartEvent(int ContentIndex, AssistantMessage Partial)
    : AssistantMessageEvent("text_start");

public sealed record TextDeltaEvent(int ContentIndex, string Delta, AssistantMessage Partial)
    : AssistantMessageEvent("text_delta");

public sealed record TextEndEvent(int ContentIndex, string Content, AssistantMessage Partial)
    : AssistantMessageEvent("text_end");

// ── Thinking streaming ───────────────────────────────
public sealed record ThinkingStartEvent(int ContentIndex, AssistantMessage Partial)
    : AssistantMessageEvent("thinking_start");

public sealed record ThinkingDeltaEvent(int ContentIndex, string Delta, AssistantMessage Partial)
    : AssistantMessageEvent("thinking_delta");

public sealed record ThinkingEndEvent(int ContentIndex, string Content, AssistantMessage Partial)
    : AssistantMessageEvent("thinking_end");

// ── Tool call streaming ──────────────────────────────
public sealed record ToolCallStartEvent(int ContentIndex, AssistantMessage Partial)
    : AssistantMessageEvent("toolcall_start");

public sealed record ToolCallDeltaEvent(int ContentIndex, string Delta, AssistantMessage Partial)
    : AssistantMessageEvent("toolcall_delta");

public sealed record ToolCallEndEvent(int ContentIndex, ToolCallContent ToolCall, AssistantMessage Partial)
    : AssistantMessageEvent("toolcall_end");
```

### Event Sequences

**Simple text response:**
```
StartEvent
  └─ TextStartEvent(index=0)
       └─ TextDeltaEvent(index=0, "Hello ")
       └─ TextDeltaEvent(index=0, "world!")
       └─ TextEndEvent(index=0, "Hello world!")
DoneEvent(Stop)
```

**Thinking + text (reasoning model):**
```
StartEvent
  ├─ ThinkingStartEvent(index=0)
  │    └─ ThinkingDeltaEvent(index=0, "Let me think...")
  │    └─ ThinkingEndEvent(index=0, "Let me think about this...")
  └─ TextStartEvent(index=1)
       └─ TextDeltaEvent(index=1, "The answer is 42.")
       └─ TextEndEvent(index=1, "The answer is 42.")
DoneEvent(Stop)
```

**Tool call:**
```
StartEvent
  └─ ToolCallStartEvent(index=0)
       └─ ToolCallDeltaEvent(index=0, "{\"path\":")
       └─ ToolCallDeltaEvent(index=0, " \"src/main.cs\"}")
       └─ ToolCallEndEvent(index=0, ToolCallContent{Id, Name, Arguments})
DoneEvent(ToolUse)
```

**Multiple tool calls:**
```
StartEvent
  ├─ ToolCallStartEvent(index=0)
  │    └─ ToolCallDeltaEvent / ToolCallEndEvent
  └─ ToolCallStartEvent(index=1)
       └─ ToolCallDeltaEvent / ToolCallEndEvent
DoneEvent(ToolUse)
```

> **Key Takeaway:** `ContentIndex` identifies which content block is being streamed. Multiple blocks can be streamed sequentially within one response. Every event includes the accumulated `Partial` message, so consumers always have the full picture.

## Stream Options

```csharp
public record class StreamOptions
{
    public float? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public string? ApiKey { get; init; }                          // Per-request API key
    public Transport Transport { get; init; } = Transport.Sse;    // SSE, WebSocket, or Auto
    public CacheRetention CacheRetention { get; init; } = CacheRetention.Short;
    public string? SessionId { get; init; }
    public Func<object, LlmModel, Task<object?>>? OnPayload { get; init; }  // Request interceptor
    public Dictionary<string, string>? Headers { get; init; }     // Custom headers
    public int MaxRetryDelayMs { get; init; } = 60000;
    public Dictionary<string, object>? Metadata { get; init; }
}

// Extended options with reasoning support
public record class SimpleStreamOptions : StreamOptions
{
    public ThinkingLevel? Reasoning { get; init; }        // Minimal, Low, Medium, High, ExtraHigh
    public ThinkingBudgets? ThinkingBudgets { get; init; }  // Custom per-level token budgets
}
```

## How a Provider Works: Anthropic Example

The Anthropic provider is a complete, production-grade implementation. Here's how it works end-to-end.

### 1. Entry Point

```csharp
public sealed partial class AnthropicProvider(HttpClient httpClient) : IApiProvider
{
    public string Api => "anthropic-messages";

    public LlmStream Stream(LlmModel model, Context context, StreamOptions? options = null)
    {
        var stream = new LlmStream();
        var contentBlocks = new List<ContentBlock>();
        var usage = Usage.Empty();

        // Fire-and-forget streaming task
        _ = Task.Run(async () =>
        {
            try
            {
                await StreamCoreAsync(model, context, options, stream,
                    contentBlocks, usage, ...);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var errorMessage = BuildMessage(model, contentBlocks, usage,
                    StopReason.Error, ex.Message, null);
                stream.Push(new ErrorEvent(StopReason.Error, errorMessage));
            }
        });

        return stream;
    }
}
```

> **Key Takeaway:** The provider returns `LlmStream` immediately and streams events via a background task. Errors are never thrown — they're encoded as `ErrorEvent`.

### 2. SSE Streaming

The core streaming method sends an HTTP request and parses the SSE response line by line:

```csharp
private async Task StreamCoreAsync(...)
{
    // 1. Build request body (model, messages, tools, thinking config)
    var body = BuildRequestBody(model, context, options, anthropicOpts);

    // 2. Configure headers (auth mode, beta features, version)
    var request = new HttpRequestMessage(HttpMethod.Post, url);
    ConfigureRequestHeaders(request, apiKey, authMode, anthropicOpts, model);

    // 3. Send with streaming
    var response = await httpClient.SendAsync(request,
        HttpCompletionOption.ResponseHeadersRead, ct);

    // 4. Parse SSE line by line
    using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
    while (!reader.EndOfStream)
    {
        var line = await reader.ReadLineAsync(ct);
        if (line?.StartsWith("data: ") == true)
        {
            var json = JsonDocument.Parse(line.AsSpan(6));
            ProcessSseEvent(json, stream, contentBlocks, ...);
        }
    }

    // 5. Push final DoneEvent
    var finalMessage = BuildMessage(model, contentBlocks, usage, stopReason, null, responseId);
    stream.Push(new DoneEvent(stopReason, finalMessage));
    stream.End(finalMessage);
}
```

### 3. Message Conversion

Providers must convert BotNexus messages to their API format:

```csharp
// BotNexus UserMessage → Anthropic format
private static Dictionary<string, object?> ConvertUserMessage(UserMessage msg)
{
    return new Dictionary<string, object?>
    {
        ["role"] = "user",
        ["content"] = msg.Content.IsText
            ? msg.Content.Text                              // Simple string
            : ConvertContentBlocks(msg.Content.Blocks!)     // Multi-block array
    };
}

// BotNexus ToolResultMessage → Anthropic tool_result block
private static Dictionary<string, object?> MakeToolResultBlock(ToolResultMessage toolResult)
{
    return new Dictionary<string, object?>
    {
        ["type"] = "tool_result",
        ["tool_use_id"] = toolResult.ToolCallId,
        ["content"] = ExtractContent(toolResult),
        ["is_error"] = toolResult.IsError ? true : null
    };
}
```

### 4. Authentication

The Anthropic provider supports three auth modes:

```csharp
private enum AuthMode { ApiKey, OAuth, Copilot }

private static AuthMode DetectAuthMode(string? apiKey, LlmModel model)
{
    if (model.Provider == "github-copilot") return AuthMode.Copilot;
    if (apiKey?.StartsWith("sk-ant-oat") == true) return AuthMode.OAuth;
    return AuthMode.ApiKey;
}
```

Each mode sets different headers:
- **ApiKey:** `x-api-key: sk-ant-...`
- **OAuth:** `Authorization: Bearer sk-ant-oat-...`
- **Copilot:** `Authorization: Bearer <copilot-token>` + dynamic Copilot headers

## API Key Resolution

When no explicit key is provided, `EnvironmentApiKeys` checks environment variables:

| Provider | Environment Variables (priority order) |
|----------|---------------------------------------|
| `github-copilot` | `COPILOT_GITHUB_TOKEN` → `GH_TOKEN` → `GITHUB_TOKEN` |
| `anthropic` | `ANTHROPIC_OAUTH_TOKEN` → `ANTHROPIC_API_KEY` |
| `openai` | `OPENAI_API_KEY` |
| `google` | `GEMINI_API_KEY` |
| `groq` | `GROQ_API_KEY` |
| `xai` | `XAI_API_KEY` |

## Error Handling: Never Throw from Stream

Providers follow a strict rule: **never throw exceptions that escape the stream**.

```csharp
// ✅ Correct: encode errors as events
try
{
    await StreamCoreAsync(...);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    stream.Push(new ErrorEvent(StopReason.Error, errorMessage));
    stream.End(errorMessage);
}

// ❌ Wrong: unhandled exceptions break the consumer
public LlmStream Stream(...)
{
    ValidateApiKey();  // This could throw!
    // ...
}
```

The agent loop expects to consume the stream without catching provider exceptions. All errors must be communicated through `ErrorEvent`.

## Context Overflow Detection

`ContextOverflowDetector` uses regex patterns to detect when an LLM rejects a request due to context length:

```csharp
// Checks error messages for patterns like:
// "prompt is too long", "exceeds the context window",
// "maximum context length is N tokens", etc.
bool isOverflow = ContextOverflowDetector.IsContextOverflow(errorMessage);
```

When detected, the agent can trigger session compaction to reduce context size.

## What's Next

- **[Agent Core](03-agent-core.md)** — How the agent loop consumes streams and drives tool execution
- **[Add a Provider](06-adding-a-provider.md)** — Step-by-step tutorial for implementing a new provider
