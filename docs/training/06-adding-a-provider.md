# 06 — Tutorial: Add a New LLM Provider

This tutorial walks through implementing a complete LLM provider for a hypothetical "MyLLM" API. By the end, you'll have a provider that streams responses, handles tool calls, and integrates with the agent infrastructure.

## What You're Building

A provider that implements `IApiProvider` for "MyLLM" — an API that supports:
- Chat completions with streaming (SSE)
- Tool calling
- System prompts

The same pattern works for any LLM API.

## Step 1: Create the Project

```bash
dotnet new classlib -n BotNexus.Providers.MyLLM
cd BotNexus.Providers.MyLLM
dotnet add reference ../BotNexus.Providers.Core/BotNexus.Providers.Core.csproj
```

Your csproj:
```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include="..\BotNexus.Providers.Core\BotNexus.Providers.Core.csproj" />
    </ItemGroup>
</Project>
```

## Step 2: Implement IApiProvider

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BotNexus.Providers.Core;
using BotNexus.Providers.Core.Models;
using BotNexus.Providers.Core.Registry;
using BotNexus.Providers.Core.Streaming;

namespace BotNexus.Providers.MyLLM;

public sealed class MyLlmProvider(HttpClient httpClient) : IApiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Unique API identifier — used for routing
    public string Api => "myllm-chat";

    public LlmStream Stream(LlmModel model, Context context, StreamOptions? options = null)
    {
        var stream = new LlmStream();
        var contentBlocks = new List<ContentBlock>();
        var usage = Usage.Empty();
        StopReason stopReason = StopReason.Stop;
        string? responseId = null;

        // Fire-and-forget: stream events in background
        _ = Task.Run(async () =>
        {
            try
            {
                await StreamCoreAsync(
                    model, context, options, stream,
                    contentBlocks, ref usage, ref stopReason, ref responseId,
                    options?.CancellationToken ?? CancellationToken.None);

                // Build final message and push Done
                var finalMessage = BuildFinalMessage(
                    model, contentBlocks, usage, stopReason, responseId);
                stream.Push(new DoneEvent(stopReason, finalMessage));
                stream.End(finalMessage);
            }
            catch (OperationCanceledException)
            {
                var aborted = BuildFinalMessage(
                    model, contentBlocks, usage, StopReason.Aborted, responseId);
                stream.Push(new DoneEvent(StopReason.Aborted, aborted));
                stream.End(aborted);
            }
            catch (Exception ex)
            {
                // CRITICAL: Never throw from stream — encode as ErrorEvent
                var errorMessage = BuildFinalMessage(
                    model, contentBlocks, usage, StopReason.Error, responseId, ex.Message);
                stream.Push(new ErrorEvent(StopReason.Error, errorMessage));
                stream.End(errorMessage);
            }
        });

        return stream;
    }

    public LlmStream StreamSimple(
        LlmModel model, Context context, SimpleStreamOptions? options = null)
    {
        // Convert SimpleStreamOptions to your format
        // Map reasoning levels to provider-specific config
        var streamOptions = new StreamOptions
        {
            Temperature = options?.Temperature,
            MaxTokens = options?.MaxTokens,
            CancellationToken = options?.CancellationToken ?? default,
            ApiKey = options?.ApiKey,
            CacheRetention = options?.CacheRetention ?? CacheRetention.Short,
            Headers = options?.Headers
        };

        return Stream(model, context, streamOptions);
    }
}
```

> **Key Takeaway:** Return the `LlmStream` immediately. Do all work in the background task. Never let exceptions escape — always encode them as `ErrorEvent`.

## Step 3: Handle SSE Streaming

The core streaming method sends an HTTP request and processes the SSE response:

```csharp
private async Task StreamCoreAsync(
    LlmModel model, Context context, StreamOptions? options,
    LlmStream stream, List<ContentBlock> contentBlocks,
    ref Usage usage, ref StopReason stopReason, ref string? responseId,
    CancellationToken ct)
{
    // 1. Resolve API key
    var apiKey = options?.ApiKey
        ?? EnvironmentApiKeys.GetApiKey(model.Provider)
        ?? throw new InvalidOperationException(
            $"No API key found for provider: {model.Provider}");

    // 2. Build the request body
    var body = BuildRequestBody(model, context, options);
    var json = JsonSerializer.Serialize(body, JsonOptions);

    // 3. Create HTTP request
    var url = $"{model.BaseUrl}/v1/chat/completions";
    var request = new HttpRequestMessage(HttpMethod.Post, url)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

    // 4. Send with streaming
    var response = await httpClient.SendAsync(
        request, HttpCompletionOption.ResponseHeadersRead, ct);
    response.EnsureSuccessStatusCode();

    // 5. Push StartEvent
    var partial = BuildPartialMessage(model, contentBlocks, usage);
    stream.Push(new StartEvent(partial));

    // 6. Parse SSE response line by line
    using var responseStream = await response.Content.ReadAsStreamAsync(ct);
    using var reader = new StreamReader(responseStream);

    var textBuilder = new StringBuilder();
    var toolJsonBuilder = new StringBuilder();
    int contentIndex = 0;
    string? currentToolId = null;
    string? currentToolName = null;

    while (!reader.EndOfStream)
    {
        var line = await reader.ReadLineAsync(ct);

        // SSE format: "data: {json}" or "data: [DONE]"
        if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
            continue;

        var data = line.AsSpan(6);  // Skip "data: "

        if (data is "[DONE]")
            break;

        using var doc = JsonDocument.Parse(data.ToString());
        var root = doc.RootElement;

        // Extract response ID
        if (root.TryGetProperty("id", out var idProp))
            responseId = idProp.GetString();

        // Extract usage (if present in final chunk)
        if (root.TryGetProperty("usage", out var usageProp))
            usage = ParseUsage(usageProp);

        // Process choices
        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            continue;

        var choice = choices[0];
        var delta = choice.GetProperty("delta");

        // Check finish reason
        if (choice.TryGetProperty("finish_reason", out var finishProp)
            && finishProp.ValueKind != JsonValueKind.Null)
        {
            stopReason = MapFinishReason(finishProp.GetString());
        }

        // Process content delta (text)
        if (delta.TryGetProperty("content", out var contentProp)
            && contentProp.ValueKind == JsonValueKind.String)
        {
            var text = contentProp.GetString()!;

            if (textBuilder.Length == 0)
            {
                // First text chunk — emit TextStart
                partial = BuildPartialMessage(model, contentBlocks, usage);
                stream.Push(new TextStartEvent(contentIndex, partial));
            }

            textBuilder.Append(text);
            partial = BuildPartialMessage(model, contentBlocks, usage);
            stream.Push(new TextDeltaEvent(contentIndex, text, partial));
        }

        // Process tool calls delta
        if (delta.TryGetProperty("tool_calls", out var toolCallsProp))
        {
            foreach (var tc in toolCallsProp.EnumerateArray())
            {
                if (tc.TryGetProperty("function", out var fn))
                {
                    // New tool call
                    if (fn.TryGetProperty("name", out var nameProp))
                    {
                        // Finalize previous text block if any
                        if (textBuilder.Length > 0)
                        {
                            var fullText = textBuilder.ToString();
                            contentBlocks.Add(new TextContent(fullText));
                            partial = BuildPartialMessage(model, contentBlocks, usage);
                            stream.Push(new TextEndEvent(contentIndex, fullText, partial));
                            textBuilder.Clear();
                            contentIndex++;
                        }

                        currentToolName = nameProp.GetString();
                        currentToolId = tc.TryGetProperty("id", out var idP)
                            ? idP.GetString() : Guid.NewGuid().ToString();
                        toolJsonBuilder.Clear();

                        partial = BuildPartialMessage(model, contentBlocks, usage);
                        stream.Push(new ToolCallStartEvent(contentIndex, partial));
                    }

                    // Tool arguments delta
                    if (fn.TryGetProperty("arguments", out var argsProp)
                        && argsProp.ValueKind == JsonValueKind.String)
                    {
                        var argsDelta = argsProp.GetString()!;
                        toolJsonBuilder.Append(argsDelta);

                        partial = BuildPartialMessage(model, contentBlocks, usage);
                        stream.Push(new ToolCallDeltaEvent(contentIndex, argsDelta, partial));
                    }
                }
            }
        }
    }

    // Finalize any remaining text block
    if (textBuilder.Length > 0)
    {
        var fullText = textBuilder.ToString();
        contentBlocks.Add(new TextContent(fullText));
        partial = BuildPartialMessage(model, contentBlocks, usage);
        stream.Push(new TextEndEvent(contentIndex, fullText, partial));
    }

    // Finalize any remaining tool call
    if (currentToolId is not null)
    {
        var argsJson = toolJsonBuilder.ToString();
        var args = string.IsNullOrEmpty(argsJson)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson, JsonOptions)
              ?? new Dictionary<string, object?>();

        var toolCall = new ToolCallContent(currentToolId, currentToolName!, args);
        contentBlocks.Add(toolCall);

        partial = BuildPartialMessage(model, contentBlocks, usage);
        stream.Push(new ToolCallEndEvent(contentIndex, toolCall, partial));
    }
}
```

## Step 4: Message Conversion

Convert BotNexus messages to your API format:

```csharp
private static Dictionary<string, object?> BuildRequestBody(
    LlmModel model, Context context, StreamOptions? options)
{
    var body = new Dictionary<string, object?>
    {
        ["model"] = model.Id,
        ["stream"] = true,
        ["stream_options"] = new Dictionary<string, object?> { ["include_usage"] = true },
        ["max_tokens"] = options?.MaxTokens ?? model.MaxTokens / 3,
    };

    if (options?.Temperature is not null)
        body["temperature"] = options.Temperature;

    // System prompt
    var messages = new List<Dictionary<string, object?>>();
    if (context.SystemPrompt is not null)
    {
        messages.Add(new Dictionary<string, object?>
        {
            ["role"] = "system",
            ["content"] = context.SystemPrompt
        });
    }

    // Convert messages
    foreach (var msg in context.Messages)
    {
        switch (msg)
        {
            case UserMessage user:
                messages.Add(new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = user.Content.IsText
                        ? user.Content.Text
                        : ConvertContentBlocks(user.Content.Blocks!)
                });
                break;

            case AssistantMessage assistant:
                var assistantMsg = new Dictionary<string, object?>
                {
                    ["role"] = "assistant",
                };
                // Extract text and tool calls
                var textParts = assistant.Content.OfType<TextContent>().ToList();
                var toolCalls = assistant.Content.OfType<ToolCallContent>().ToList();

                if (textParts.Any())
                    assistantMsg["content"] = string.Join("\n", textParts.Select(t => t.Text));
                if (toolCalls.Any())
                    assistantMsg["tool_calls"] = toolCalls.Select(tc =>
                        new Dictionary<string, object?>
                        {
                            ["id"] = tc.Id,
                            ["type"] = "function",
                            ["function"] = new Dictionary<string, object?>
                            {
                                ["name"] = tc.Name,
                                ["arguments"] = JsonSerializer.Serialize(tc.Arguments, JsonOptions)
                            }
                        }).ToList();
                messages.Add(assistantMsg);
                break;

            case ToolResultMessage toolResult:
                messages.Add(new Dictionary<string, object?>
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = toolResult.ToolCallId,
                    ["content"] = string.Join("\n",
                        toolResult.Content.OfType<TextContent>().Select(t => t.Text))
                });
                break;
        }
    }

    body["messages"] = messages;

    // Convert tools
    if (context.Tools?.Count > 0)
    {
        body["tools"] = context.Tools.Select(t => new Dictionary<string, object?>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object?>
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["parameters"] = JsonSerializer.Deserialize<object>(t.Parameters.GetRawText())
            }
        }).ToList();
    }

    return body;
}
```

## Step 5: Helper Methods

```csharp
private static StopReason MapFinishReason(string? reason) => reason switch
{
    "stop" => StopReason.Stop,
    "length" => StopReason.Length,
    "tool_calls" => StopReason.ToolUse,
    "content_filter" => StopReason.Refusal,
    _ => StopReason.Stop
};

private static Usage ParseUsage(JsonElement usage) => new()
{
    Input = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0,
    Output = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0,
    TotalTokens = usage.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0
};

private static AssistantMessage BuildPartialMessage(
    LlmModel model, List<ContentBlock> contentBlocks, Usage usage)
{
    return new AssistantMessage(
        Content: contentBlocks.ToList(),
        Api: "myllm-chat",
        Provider: model.Provider,
        ModelId: model.Id,
        Usage: usage,
        StopReason: StopReason.Stop,
        ErrorMessage: null,
        ResponseId: null,
        Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    );
}

private static AssistantMessage BuildFinalMessage(
    LlmModel model, List<ContentBlock> contentBlocks, Usage usage,
    StopReason stopReason, string? responseId, string? errorMessage = null)
{
    var totalTokens = usage.Input + usage.Output + usage.CacheRead + usage.CacheWrite;
    var cost = ModelRegistry.CalculateCost(model, usage);

    return new AssistantMessage(
        Content: contentBlocks.ToList(),
        Api: "myllm-chat",
        Provider: model.Provider,
        ModelId: model.Id,
        Usage: usage with { TotalTokens = totalTokens, Cost = cost },
        StopReason: stopReason,
        ErrorMessage: errorMessage,
        ResponseId: responseId,
        Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    );
}

private static object ConvertContentBlocks(IReadOnlyList<ContentBlock> blocks)
{
    return blocks.Select(b => b switch
    {
        TextContent text => (object)new Dictionary<string, object?>
        {
            ["type"] = "text",
            ["text"] = text.Text
        },
        ImageContent image => new Dictionary<string, object?>
        {
            ["type"] = "image_url",
            ["image_url"] = new Dictionary<string, object?>
            {
                ["url"] = $"data:{image.MimeType};base64,{image.Data}"
            }
        },
        _ => new Dictionary<string, object?> { ["type"] = "text", ["text"] = b.ToString()! }
    }).ToList();
}
```

## Step 6: Register in ApiProviderRegistry

```csharp
var apiRegistry = new ApiProviderRegistry();
var httpClient = new HttpClient();

// Register your provider
apiRegistry.Register(new MyLlmProvider(httpClient));
```

## Step 7: Add Model Definitions to ModelRegistry

```csharp
var modelRegistry = new ModelRegistry();

modelRegistry.Register("myllm", new LlmModel(
    Id: "myllm-large",
    Name: "MyLLM Large",
    Api: "myllm-chat",                 // Must match MyLlmProvider.Api
    Provider: "myllm",
    BaseUrl: "https://api.myllm.com",
    Reasoning: false,
    Input: new[] { "text", "image" },
    Cost: new ModelCost(
        Input: 2.0m,                   // $2 per million input tokens
        Output: 8.0m,                  // $8 per million output tokens
        CacheRead: 0.2m,
        CacheWrite: 2.5m
    ),
    ContextWindow: 128_000,
    MaxTokens: 32_000
));

modelRegistry.Register("myllm", new LlmModel(
    Id: "myllm-fast",
    Name: "MyLLM Fast",
    Api: "myllm-chat",
    Provider: "myllm",
    BaseUrl: "https://api.myllm.com",
    Reasoning: false,
    Input: new[] { "text" },
    Cost: new ModelCost(0.5m, 2.0m, 0.05m, 0.625m),
    ContextWindow: 64_000,
    MaxTokens: 16_000
));
```

## Step 8: Add API Key Resolution

Register your provider's environment variable in `EnvironmentApiKeys`:

```csharp
// In EnvironmentApiKeys.cs, add to the EnvMap dictionary:
["myllm"] = "MYLLM_API_KEY",
```

Or handle it in your provider directly:

```csharp
var apiKey = options?.ApiKey
    ?? Environment.GetEnvironmentVariable("MYLLM_API_KEY")
    ?? throw new InvalidOperationException("MYLLM_API_KEY not set");
```

## Step 9: Test with Existing Infrastructure

```csharp
// Full integration test
var apiRegistry = new ApiProviderRegistry();
apiRegistry.Register(new MyLlmProvider(new HttpClient()));

var modelRegistry = new ModelRegistry();
modelRegistry.Register("myllm", new LlmModel(
    Id: "myllm-large", Name: "MyLLM Large", Api: "myllm-chat",
    Provider: "myllm", BaseUrl: "https://api.myllm.com",
    Reasoning: false, Input: new[] { "text" },
    Cost: new ModelCost(2m, 8m, 0.2m, 2.5m),
    ContextWindow: 128_000, MaxTokens: 32_000
));

var llmClient = new LlmClient(apiRegistry, modelRegistry);
var model = modelRegistry.GetModel("myllm", "myllm-large")!;

// Test 1: Simple streaming
var context = new Context(
    SystemPrompt: "You are a helpful assistant.",
    Messages: new Message[]
    {
        new UserMessage("Hello, how are you?",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
    }
);

var stream = llmClient.Stream(model, context);
await foreach (var evt in stream)
{
    Console.WriteLine($"Event: {evt.Type}");
    if (evt is TextDeltaEvent delta)
        Console.Write(delta.Delta);
    if (evt is ErrorEvent error)
        Console.WriteLine($"Error: {error.Error.ErrorMessage}");
}

// Test 2: One-shot completion
var result = await llmClient.CompleteAsync(model, context);
Console.WriteLine($"\nResult: {result.Content.OfType<TextContent>().First().Text}");
Console.WriteLine($"Tokens: {result.Usage.TotalTokens}");
Console.WriteLine($"Cost: ${result.Usage.Cost.Total:F4}");

// Test 3: Tool calling
var toolContext = new Context(
    SystemPrompt: "Use the calculator tool to answer math questions.",
    Messages: new Message[]
    {
        new UserMessage("What is 42 * 17?",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
    },
    Tools: new Tool[]
    {
        new Tool("calculator", "Perform arithmetic", JsonDocument.Parse("""
            { "type": "object", "properties": {
                "expression": { "type": "string" }
            }, "required": ["expression"] }
        """).RootElement)
    }
);

var toolStream = llmClient.Stream(model, toolContext);
await foreach (var evt in toolStream)
{
    if (evt is ToolCallEndEvent toolEnd)
    {
        Console.WriteLine($"Tool call: {toolEnd.ToolCall.Name}({toolEnd.ToolCall.Arguments})");
    }
}

// Test 4: Full agent integration
var agent = new Agent(new AgentOptions(
    InitialState: new AgentInitialState(
        SystemPrompt: "You are helpful.",
        Model: model
    ),
    Model: model,
    LlmClient: llmClient,
    ConvertToLlm: MessageConverter.ToProviderMessages,
    TransformContext: (ctx, ct) => ValueTask.FromResult(ctx),
    GetApiKey: (provider, ct) =>
        ValueTask.FromResult(Environment.GetEnvironmentVariable("MYLLM_API_KEY")),
    GetSteeringMessages: null,
    GetFollowUpMessages: null,
    ToolExecutionMode: ToolExecutionMode.Sequential,
    BeforeToolCall: null,
    AfterToolCall: null,
    GenerationSettings: new SimpleStreamOptions(),
    SteeringMode: QueueMode.All,
    FollowUpMode: QueueMode.OneAtATime
));

var messages = await agent.PromptAsync("Hello!");
Console.WriteLine($"Agent produced {messages.Count} messages");
```

## Provider Checklist

Before shipping your provider, verify:

- [ ] `Api` property returns a unique, stable identifier
- [ ] `Stream` returns `LlmStream` immediately (no blocking)
- [ ] Errors are encoded as `ErrorEvent`, never thrown
- [ ] `OperationCanceledException` produces `StopReason.Aborted`
- [ ] Event sequence: `StartEvent` → content events → `DoneEvent`/`ErrorEvent`
- [ ] `ContentIndex` increments correctly across content blocks
- [ ] Tool calls produce `ToolCallStartEvent` → deltas → `ToolCallEndEvent`
- [ ] `ToolCallEndEvent` includes complete, parsed arguments
- [ ] `DoneEvent.Message` includes final usage and cost
- [ ] `StopReason` maps correctly from provider's finish reason
- [ ] API key resolved from options, environment, or throws clear error
- [ ] `CancellationToken` is respected throughout

## What's Next

- **[Provider System](02-provider-system.md)** — Deep dive into the streaming protocol
- **[Architecture Overview](01-architecture-overview.md)** — How providers fit in the big picture
