# 05 — Tutorial: Build a Custom Agent

This tutorial walks through building a custom agent from scratch — a Q&A agent that can answer questions and query a database.

## Step 1: Create a Minimal Agent

Let's start with the simplest possible agent: one that answers questions with no tools.

### Project Setup

```bash
dotnet new classlib -n MyAgent
cd MyAgent
dotnet add reference ../src/agent/BotNexus.AgentCore/BotNexus.AgentCore.csproj
dotnet add reference ../src/providers/BotNexus.Providers.Core/BotNexus.Providers.Core.csproj
dotnet add reference ../src/providers/BotNexus.Providers.Anthropic/BotNexus.Providers.Anthropic.csproj
```

### Wire the Agent

```csharp
using BotNexus.AgentCore;
using BotNexus.AgentCore.Configuration;
using BotNexus.AgentCore.Loop;
using BotNexus.AgentCore.Types;
using BotNexus.Providers.Core;
using BotNexus.Providers.Core.Models;
using BotNexus.Providers.Core.Registry;
using BotNexus.Providers.Core.Streaming;

// 1. Set up registries
var apiRegistry = new ApiProviderRegistry();
var modelRegistry = new ModelRegistry();
var httpClient = new HttpClient();

// 2. Register a provider
apiRegistry.Register(new AnthropicProvider(httpClient));

// 3. Register a model
var model = new LlmModel(
    Id: "claude-sonnet-4",
    Name: "Claude Sonnet 4",
    Api: "anthropic-messages",
    Provider: "anthropic",
    BaseUrl: "https://api.anthropic.com",
    Reasoning: true,
    Input: new[] { "text" },
    Cost: new ModelCost(3m, 15m, 0.3m, 3.75m),
    ContextWindow: 200_000,
    MaxTokens: 64_000
);
modelRegistry.Register("anthropic", model);

// 4. Create the LLM client
var llmClient = new LlmClient(apiRegistry, modelRegistry);

// 5. Build agent options
var agent = new Agent(new AgentOptions(
    InitialState: new AgentInitialState(
        SystemPrompt: "You are a helpful Q&A assistant. Answer concisely.",
        Model: model,
        Tools: Array.Empty<IAgentTool>()
    ),
    Model: model,
    LlmClient: llmClient,
    ConvertToLlm: MessageConverter.ToProviderMessages,
    TransformContext: (ctx, ct) => ValueTask.FromResult(ctx),
    GetApiKey: (provider, ct) =>
        ValueTask.FromResult(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")),
    GetSteeringMessages: null,
    GetFollowUpMessages: null,
    ToolExecutionMode: ToolExecutionMode.Sequential,
    BeforeToolCall: null,
    AfterToolCall: null,
    GenerationSettings: new SimpleStreamOptions
    {
        Reasoning = ThinkingLevel.Low,
        CacheRetention = CacheRetention.Short
    },
    SteeringMode: QueueMode.All,
    FollowUpMode: QueueMode.OneAtATime
));

// 6. Subscribe to streaming events
agent.Subscribe(async (evt, ct) =>
{
    if (evt is MessageUpdateEvent update && update.ContentDelta is not null)
        Console.Write(update.ContentDelta);
});

// 7. Prompt the agent
var result = await agent.PromptAsync("What is the capital of France?");
Console.WriteLine();
Console.WriteLine($"[{result.Count} messages, status: {agent.Status}]");
```

That's a working agent. It connects to Anthropic, streams the response, and prints it character by character.

## Step 2: Add a Custom Tool

Let's add a database query tool. This shows the full `IAgentTool` implementation pattern.

### Define the Tool

```csharp
using System.Text.Json;
using BotNexus.AgentCore.Tools;
using BotNexus.AgentCore.Types;
using BotNexus.Providers.Core.Models;

public sealed class DatabaseQueryTool : IAgentTool
{
    private readonly string _connectionString;

    public DatabaseQueryTool(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ── Identity ─────────────────────────────────────────

    public string Name => "query_database";

    public string Label => "Database Query";

    public Tool Definition => new(
        Name: "query_database",
        Description: "Execute a read-only SQL query against the application database. " +
                     "Use this to look up data, check schemas, or verify records.",
        Parameters: JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "sql": {
                    "type": "string",
                    "description": "The SQL query to execute. Must be a SELECT statement."
                },
                "max_rows": {
                    "type": "integer",
                    "description": "Maximum rows to return (default: 50)"
                }
            },
            "required": ["sql"]
        }
        """).RootElement
    );

    // ── Lifecycle ────────────────────────────────────────

    public Task<IReadOnlyDictionary<string, object?>> PrepareArgumentsAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        // Validate: must have "sql" argument
        if (!arguments.TryGetValue("sql", out var sqlObj) || sqlObj is not string sql)
            throw new ArgumentException("Missing required argument: sql");

        // Safety: only allow SELECT statements
        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only SELECT statements are allowed");

        // Normalize max_rows
        var maxRows = 50;
        if (arguments.TryGetValue("max_rows", out var maxObj) && maxObj is JsonElement je)
            maxRows = je.GetInt32();

        return Task.FromResult<IReadOnlyDictionary<string, object?>>(
            new Dictionary<string, object?>
            {
                ["sql"] = sql,
                ["max_rows"] = maxRows
            });
    }

    public async Task<AgentToolResult> ExecuteAsync(
        string toolCallId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default,
        AgentToolUpdateCallback? onUpdate = null)
    {
        var sql = (string)arguments["sql"]!;
        var maxRows = (int)arguments["max_rows"]!;

        // Execute query (using your preferred database library)
        var results = await ExecuteQueryAsync(sql, maxRows, cancellationToken);

        return new AgentToolResult(
            Content: new[]
            {
                new AgentToolContent(AgentToolContentType.Text, results)
            }
        );
    }

    // ── Prompt contributions ─────────────────────────────

    public string? GetPromptSnippet() =>
        "query_database — Run read-only SQL queries against the app database";

    public IReadOnlyList<string> GetPromptGuidelines() => new[]
    {
        "Use query_database for data lookups instead of guessing values.",
        "Always use parameterized queries when interpolating user input.",
        "Limit results with max_rows to avoid overwhelming context."
    };

    // ── Private ──────────────────────────────────────────

    private async Task<string> ExecuteQueryAsync(
        string sql, int maxRows, CancellationToken ct)
    {
        // Your database implementation here.
        // Return formatted results as a string table.
        return "| id | name    | email           |\n" +
               "|----|---------|------------------|\n" +
               "| 1  | Alice   | alice@example.com |";
    }
}
```

### Register the Tool

```csharp
var dbTool = new DatabaseQueryTool("Server=localhost;Database=myapp");

var agent = new Agent(new AgentOptions(
    InitialState: new AgentInitialState(
        SystemPrompt: "You are a helpful assistant with database access.",
        Model: model,
        Tools: new IAgentTool[] { dbTool }    // <-- Register here
    ),
    // ... rest of options
));
```

Now the agent can call `query_database` when it needs data. The LLM sees the tool's name, description, and JSON Schema parameters, and decides when to use it.

## Step 3: Add an Extension

Extensions provide lifecycle hooks. Let's build a logging extension that tracks all tool calls.

### Implement IExtension

```csharp
using BotNexus.AgentCore.Hooks;
using BotNexus.AgentCore.Tools;
using BotNexus.AgentCore.Types;
using BotNexus.CodingAgent.Extensions;

public sealed class LoggingExtension : IExtension
{
    private readonly string _logPath;

    public LoggingExtension()
    {
        _logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".botnexus", "tool-calls.log");
    }

    public string Name => "logging";

    // No additional tools
    public IReadOnlyList<IAgentTool> GetTools() => Array.Empty<IAgentTool>();

    // Log before tool execution
    public async ValueTask<BeforeToolCallResult?> OnToolCallAsync(
        ToolCallLifecycleContext context, CancellationToken ct)
    {
        await File.AppendAllTextAsync(_logPath,
            $"[{DateTime.UtcNow:O}] CALL {context.ToolName}({FormatArgs(context.Arguments)})\n",
            ct);

        return null;  // Don't block any calls
    }

    // Log after tool execution
    public async ValueTask<AfterToolCallResult?> OnToolResultAsync(
        ToolResultLifecycleContext context, CancellationToken ct)
    {
        var status = context.IsError ? "ERROR" : "OK";
        var preview = context.Result.Content.FirstOrDefault()?.Value?[..Math.Min(100,
            context.Result.Content.FirstOrDefault()?.Value.Length ?? 0)] ?? "(empty)";

        await File.AppendAllTextAsync(_logPath,
            $"[{DateTime.UtcNow:O}] RESULT {context.ToolName} [{status}] {preview}\n",
            ct);

        return null;  // Don't transform results
    }

    // Track session start
    public async ValueTask OnSessionStartAsync(
        SessionLifecycleContext context, CancellationToken ct)
    {
        await File.AppendAllTextAsync(_logPath,
            $"[{DateTime.UtcNow:O}] SESSION START {context.Session.Id} " +
            $"model={context.ModelId}\n", ct);
    }

    public ValueTask OnSessionEndAsync(
        SessionLifecycleContext context, CancellationToken ct) =>
        ValueTask.CompletedTask;

    public ValueTask<string?> OnCompactionAsync(
        CompactionLifecycleContext context, CancellationToken ct) =>
        ValueTask.FromResult<string?>(null);

    public ValueTask<object?> OnModelRequestAsync(
        ModelRequestLifecycleContext context, CancellationToken ct) =>
        ValueTask.FromResult<object?>(null);

    private static string FormatArgs(IReadOnlyDictionary<string, object?> args) =>
        string.Join(", ", args.Select(kv => $"{kv.Key}={kv.Value}"));
}
```

### Deploy the Extension

Extensions are loaded from DLL assemblies:

```bash
# Build your extension
dotnet publish -c Release -o ~/.botnexus-agent/extensions/

# The ExtensionLoader scans *.dll files in the extensions directory
# and discovers all classes implementing IExtension
```

Or register manually when building an agent:

```csharp
var extensions = new IExtension[] { new LoggingExtension() };
var runner = new ExtensionRunner(extensions);
var extensionTools = extensions.SelectMany(e => e.GetTools()).ToList();

var agent = await CodingAgent.CreateAsync(
    config, workingDirectory, authManager, llmClient, modelRegistry,
    extensionRunner: runner,
    extensionTools: extensionTools
);
```

## Step 4: Wire It Up with CodingAgent Factory

For the full coding agent experience, use the factory:

```csharp
using BotNexus.CodingAgent;
using BotNexus.CodingAgent.Auth;
using BotNexus.CodingAgent.Extensions;
using BotNexus.Providers.Core;
using BotNexus.Providers.Core.Registry;

// 1. Load configuration
var config = CodingAgentConfig.Load(workingDirectory);

// 2. Set up registries and providers
var apiRegistry = new ApiProviderRegistry();
var modelRegistry = new ModelRegistry();
var builtInModels = new BuiltInModels();
builtInModels.RegisterAll(modelRegistry);

apiRegistry.Register(new AnthropicProvider(new HttpClient()));
// Add more providers as needed

var llmClient = new LlmClient(apiRegistry, modelRegistry);

// 3. Set up auth
var authManager = new AuthManager(config.ConfigDirectory);

// 4. Load extensions
var extensionLoader = new ExtensionLoader();
var loadResult = extensionLoader.LoadExtensions(config.ExtensionsDirectory);
var extensionRunner = new ExtensionRunner(loadResult.Extensions);

// 5. Load skills
var skillsLoader = new SkillsLoader();
var skills = skillsLoader.LoadSkills(workingDirectory, config);

// 6. Create the agent
var agent = await BotNexus.CodingAgent.CodingAgent.CreateAsync(
    config, workingDirectory, authManager, llmClient, modelRegistry,
    extensionRunner: extensionRunner,
    extensionTools: loadResult.Tools,
    skills: skills
);

// 7. Subscribe and prompt
agent.Subscribe(async (evt, ct) =>
{
    if (evt is MessageUpdateEvent update && update.ContentDelta is not null)
        Console.Write(update.ContentDelta);
});

await agent.PromptAsync("Read the project structure and summarize what this codebase does.");
```

## Step 5: Test End-to-End

### Manual Testing

```csharp
// Test tool execution directly
var dbTool = new DatabaseQueryTool("Server=localhost;Database=myapp");

// Test argument validation
var validated = await dbTool.PrepareArgumentsAsync(
    new Dictionary<string, object?> { ["sql"] = "SELECT * FROM users LIMIT 5" });

// Test execution
var result = await dbTool.ExecuteAsync("test-call-1", validated);
Console.WriteLine(result.Content[0].Value);
```

### Test the Agent Loop

```csharp
// Create agent with your custom tool
var agent = new Agent(new AgentOptions(
    InitialState: new AgentInitialState(
        SystemPrompt: "You are a database assistant. Use the query_database tool to answer questions.",
        Model: model,
        Tools: new IAgentTool[] { new DatabaseQueryTool(connectionString) }
    ),
    // ... options
));

// Track what happens
var events = new List<AgentEvent>();
agent.Subscribe(async (evt, ct) => events.Add(evt));

// Run
var messages = await agent.PromptAsync("How many users are in the database?");

// Verify the agent used the tool
Assert.Contains(events, e => e is ToolExecutionStartEvent t && t.ToolName == "query_database");
Assert.Contains(events, e => e is ToolExecutionEndEvent t && !t.IsError);

// Verify the final response mentions the count
var lastAssistant = messages.OfType<AssistantAgentMessage>().Last();
Assert.Contains("users", lastAssistant.Content, StringComparison.OrdinalIgnoreCase);
```

### Test Hooks

```csharp
// Test that safety hooks block dangerous queries
var blocked = false;
BeforeToolCallDelegate beforeHook = async (ctx, ct) =>
{
    if (ctx.ToolCallRequest.Name == "query_database"
        && !ctx.ValidatedArgs["sql"]!.ToString()!.TrimStart()
            .StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
    {
        blocked = true;
        return new BeforeToolCallResult(Block: true, Reason: "Only SELECT allowed");
    }
    return new BeforeToolCallResult(Block: false);
};

// Wire it up and verify
var agent = new Agent(new AgentOptions(
    BeforeToolCall: beforeHook,
    // ... rest of options
));
```

## Summary

| Step | What You Built | Key Types |
|------|---------------|-----------|
| 1 | Minimal Q&A agent | `Agent`, `AgentOptions`, `LlmClient` |
| 2 | Custom database tool | `IAgentTool`, `AgentToolResult` |
| 3 | Logging extension | `IExtension`, `ToolCallLifecycleContext` |
| 4 | Full coding agent | `CodingAgent.CreateAsync`, `ExtensionRunner` |
| 5 | End-to-end tests | `AgentEvent`, `ToolExecutionStartEvent` |

## What's Next

- **[Add a Provider](06-adding-a-provider.md)** — Implement a new LLM provider from scratch
- **[Agent Core](03-agent-core.md)** — Deep dive into the loop internals
