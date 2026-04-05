# Tool Execution

Tools are how the agent interacts with the outside world — reading files, running commands, searching code. This document covers the `IAgentTool` interface, the execution pipeline, hooks, and how to implement custom tools.

## IAgentTool — The Tool Contract

Every tool implements `IAgentTool`:

```csharp
public interface IAgentTool
{
    // Unique name exposed to the model (case-insensitive lookup)
    string Name { get; }

    // Human-readable label for logs and diagnostics
    string Label { get; }

    // JSON Schema definition sent to the LLM
    Tool Definition { get; }

    // Validate and prepare arguments before execution
    Task<IReadOnlyDictionary<string, object?>> PrepareArgumentsAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);

    // Execute the tool with validated arguments
    Task<AgentToolResult> ExecuteAsync(
        string toolCallId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default,
        AgentToolUpdateCallback? onUpdate = null);
}
```

### Tool Definition

The `Tool` record defines what the LLM sees:

```csharp
public record Tool(
    string Name,              // Tool name (must match IAgentTool.Name)
    string Description,       // Description for the model
    JsonElement Parameters    // JSON Schema for parameters
);
```

The `Parameters` field is a `JsonElement` containing a JSON Schema object. The model uses this schema to generate valid tool call arguments.

### Tool Results

Tools return `AgentToolResult`:

```csharp
public record AgentToolResult(
    IReadOnlyList<AgentToolContent> Content,  // Text or image content blocks
    object? Details = null                     // Optional metadata (not sent to LLM)
);

public record AgentToolContent(
    AgentToolContentType Type,  // Text or Image
    string Value                // The content value
);

public enum AgentToolContentType { Text, Image }
```

## Tool Execution Pipeline

The `ToolExecutor` runs tool calls through a multi-stage pipeline:

```mermaid
flowchart TD
    Start([Assistant message<br/>with tool calls]) --> FindTool{Find tool<br/>by name?}
    FindTool -->|Not found| ErrorResult1[Return error:<br/>"Tool not registered"]
    FindTool -->|Found| Prepare[PrepareArgumentsAsync<br/>validate & coerce args]
    Prepare -->|Exception| ErrorResult2[Return error:<br/>"Invalid arguments"]
    Prepare -->|Success| BeforeHook{BeforeToolCall<br/>hook?}
    BeforeHook -->|Block=true| ErrorResult3[Return error:<br/>hook reason]
    BeforeHook -->|null or Block=false| Execute[ExecuteAsync<br/>run the tool]
    Execute -->|Exception| ErrorResult4[Return error:<br/>"Tool failed"]
    Execute -->|Success| AfterHook{AfterToolCall<br/>hook?}
    AfterHook -->|null| EmitResult[Emit ToolResultMessage]
    AfterHook -->|Override| Transform[Replace Content,<br/>Details, or IsError]
    Transform --> EmitResult
    ErrorResult1 --> EmitResult
    ErrorResult2 --> EmitResult
    ErrorResult3 --> EmitResult
    ErrorResult4 --> AfterHook
    EmitResult --> Done([ToolResultAgentMessage])
```

### Pipeline Stages

1. **Tool Lookup** — find the `IAgentTool` by name (case-insensitive)
2. **Argument Preparation** — call `PrepareArgumentsAsync` to validate/coerce
3. **Before Hook** — call `BeforeToolCallDelegate` for policy enforcement
4. **Execution** — call `ExecuteAsync` with validated arguments
5. **After Hook** — call `AfterToolCallDelegate` for result transformation
6. **Result Emission** — wrap in `ToolResultAgentMessage`, emit events

## Sequential vs Parallel Execution

The `ToolExecutionMode` enum controls how multiple tool calls in a single assistant message are executed:

### Sequential (default)

```
ToolCall 1: prepare → before hook → execute → after hook → emit
ToolCall 2: prepare → before hook → execute → after hook → emit
ToolCall 3: prepare → before hook → execute → after hook → emit
```

Events are emitted in order: `Start1 → End1 → Start2 → End2 → Start3 → End3`

### Parallel

```
ToolCall 1: prepare (sequential)
ToolCall 2: prepare (sequential)
ToolCall 3: prepare (sequential)
                ↓
Emit: Start1, Start2, Start3
                ↓
ToolCall 1: before hook → execute → after hook ─┐
ToolCall 2: before hook → execute → after hook ──┤ (concurrent)
ToolCall 3: before hook → execute → after hook ─┘
                ↓
Emit: End1, End2, End3 (in original order)
```

Key differences:
- **Preparation is always sequential** (even in parallel mode)
- **Execution runs concurrently** via `Task.WhenAll`
- **End events are emitted in original order** (deterministic)

Choose parallel when tools are independent and thread-safe (e.g., multiple file reads). Use sequential when tools have side effects or shared state.

## Before/After Hooks

### BeforeToolCallDelegate

Runs before tool execution. Can inspect and block tool calls.

```csharp
public delegate Task<BeforeToolCallResult?> BeforeToolCallDelegate(
    BeforeToolCallContext context,
    CancellationToken cancellationToken);

// The context provides:
public record BeforeToolCallContext(
    AssistantAgentMessage AssistantMessage,      // The requesting message
    ToolCallContent ToolCallRequest,             // Tool call (id, name, args)
    IReadOnlyDictionary<string, object?> ValidatedArgs,  // After PrepareArguments
    AgentContext AgentContext);                   // Current agent context

// Return null to allow, or block:
public record BeforeToolCallResult(bool Block, string? Reason = null);
```

### AfterToolCallDelegate

Runs after tool execution. Can transform results.

```csharp
public delegate Task<AfterToolCallResult?> AfterToolCallDelegate(
    AfterToolCallContext context,
    CancellationToken cancellationToken);

// The context provides:
public record AfterToolCallContext(
    AssistantAgentMessage AssistantMessage,
    ToolCallContent ToolCallRequest,
    IReadOnlyDictionary<string, object?> ValidatedArgs,
    AgentToolResult Result,         // Original result
    bool IsError,                   // Whether execution failed
    AgentContext AgentContext);

// Return null to keep original, or override:
public record AfterToolCallResult(
    IReadOnlyList<AgentToolContent>? Content = null,
    object? Details = null,
    bool? IsError = null);
```

## How Tool Results Feed Back

After tools execute, their results are appended to the message timeline as `ToolResultAgentMessage` records. On the next loop iteration:

1. `ConvertToLlm` converts `ToolResultAgentMessage` → provider `ToolResultMessage`
2. The messages (including tool results) are sent to the LLM
3. The LLM sees the results and either calls more tools or produces a final response

This is the fundamental loop: **LLM → tool calls → tool results → LLM → ...**

## Example: Implementing a Custom Tool

Here's a complete example of a custom tool that searches a database:

```csharp
using System.Text.Json;
using BotNexus.AgentCore.Tools;
using BotNexus.AgentCore.Types;
using BotNexus.Providers.Core.Models;

public sealed class DatabaseSearchTool : IAgentTool
{
    private readonly string _connectionString;

    public DatabaseSearchTool(string connectionString)
    {
        _connectionString = connectionString;
    }

    public string Name => "database_search";

    public string Label => "Database Search";

    public Tool Definition => new Tool(
        Name: "database_search",
        Description: "Search the application database with a SQL query. Returns results as JSON.",
        Parameters: JsonSerializer.Deserialize<JsonElement>("""
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The SQL SELECT query to execute (read-only)"
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum rows to return (default: 100)",
                    "default": 100
                }
            },
            "required": ["query"]
        }
        """));

    public Task<IReadOnlyDictionary<string, object?>> PrepareArgumentsAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        // Validate required parameter
        if (!arguments.TryGetValue("query", out var queryObj) || queryObj is null)
        {
            throw new ArgumentException("'query' parameter is required.");
        }

        var query = queryObj.ToString()!;

        // Safety: block non-SELECT queries
        if (!query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only SELECT queries are allowed.");
        }

        return Task.FromResult(arguments);
    }

    public async Task<AgentToolResult> ExecuteAsync(
        string toolCallId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default,
        AgentToolUpdateCallback? onUpdate = null)
    {
        var query = arguments["query"]!.ToString()!;
        var limit = arguments.TryGetValue("limit", out var limitObj) && limitObj is not null
            ? Convert.ToInt32(limitObj)
            : 100;

        // Execute query and format results
        var results = await RunQueryAsync(query, limit, cancellationToken);
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        return new AgentToolResult([
            new AgentToolContent(AgentToolContentType.Text, json)
        ]);
    }

    private Task<List<Dictionary<string, object?>>> RunQueryAsync(
        string query, int limit, CancellationToken ct)
    {
        // Your database query implementation here
        throw new NotImplementedException();
    }
}
```

### Registering the Tool

Tools are provided via `AgentInitialState.Tools` or `AgentState.Tools`:

```csharp
var tools = new List<IAgentTool>
{
    new DatabaseSearchTool(connectionString),
    // ... other tools
};

var options = new AgentOptions(
    InitialState: new AgentInitialState(
        SystemPrompt: "You are a database assistant.",
        Model: model,
        Tools: tools),
    // ... rest of options
);

var agent = new Agent(options);
```

## Next Steps

- [CodingAgent Layer →](05-coding-agent.md) — see the built-in tools in action
- [Building Your Own →](06-building-your-own.md) — wire tools into a complete agent
- [Agent Loop](03-agent-loop.md) — how the loop calls ToolExecutor
