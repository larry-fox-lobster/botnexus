# BotNexus Architecture Overview

**Version:** 1.0  
**Last Updated:** 2026-04-01  
**Lead Architect:** Leela

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Component Diagram](#component-diagram)
3. [Message Flow](#message-flow)
4. [Dynamic Extension Loading](#dynamic-extension-loading)
5. [Dependency Injection](#dependency-injection)
6. [Core Abstractions](#core-abstractions)
7. [Multi-Agent Routing](#multi-agent-routing)
8. [Provider Architecture](#provider-architecture)
9. [Session Management](#session-management)
10. [Security Model](#security-model)
11. [Observability](#observability)
12. [Installation Layout](#installation-layout)

---

## 1. System Overview

**BotNexus** is a modular, extensible AI agent execution platform built in C#/.NET. It enables running multiple AI agents concurrently, each powered by configurable LLM providers, receiving messages from multiple channels (Discord, Slack, Telegram, WebSocket), and executing tools dynamically.

### Design Philosophy

- **Modular**: Core engine with pluggable channels, providers, and tools
- **Extensible**: Dynamic assembly loading with folder-based organization
- **Secure**: Extension validation, OAuth support, API key authentication
- **Observable**: Correlation IDs, health checks, activity stream for real-time monitoring
- **Resilient**: Retry logic with exponential backoff, error handling, graceful degradation

### Key Characteristics

- **Lean Core**: 17 class libraries, minimal dependencies
- **Contract-First**: Core module defines 13 interfaces; implementations in outer modules
- **Async-First**: All operations async (I/O, message processing, tool execution)
- **Configuration-Driven**: Extensions loaded only when configured; no automatic discovery
- **Session-Persistent**: Conversation history persisted to disk (JSONL format)

---

## 2. Component Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          External Clients                               │
│   Discord  │  Slack  │  Telegram  │  WebSocket  │  REST API            │
└─────────────┬──────────┬────────────┬───────────┬────────────────────────┘
              │          │            │           │
              ▼          ▼            ▼           ▼
       ┌──────────────────────────────────────────────┐
       │           Channel Implementations            │
       │  (DiscordChannel, SlackChannel, etc.)        │
       └────────────────┬─────────────────────────────┘
                        │
                        ▼
            ┌────────────────────────┐
            │   Message Bus (IPC)    │
            │  (Bounded Channel)     │
            └────────────┬───────────┘
                         │
                         ▼
        ┌────────────────────────────────────┐
        │   Gateway (Orchestrator)            │
        │  - Reads from Message Bus           │
        │  - Manages Channels                 │
        │  - Broadcasts ActivityEvents        │
        └────────┬─────────────────────────────┘
                 │
       ┌─────────┴─────────┐
       │                   │
       ▼                   ▼
  ┌─────────────┐  ┌──────────────┐
  │ AgentRouter │  │ WebUI Events │
  │  (Routes to │  │ (Activity    │
  │   agents)   │  │  Stream)     │
  └─────┬───────┘  └──────────────┘
        │
        ├─────────────────┬──────────────────┐
        │                 │                  │
        ▼                 ▼                  ▼
  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐
  │ AgentRunner1 │  │ AgentRunner2 │  │ AgentRunnerN│
  │  (per agent) │  │  (per agent) │  │ (per agent) │
  └────┬─────────┘  └────┬─────────┘  └────┬────────┘
       │                 │                  │
       ├─────────────────┼──────────────────┤
       ▼                 ▼                  ▼
  ┌──────────────────────────────────────────────┐
  │  CommandRouter (Handles /commands)           │
  └──────────────────────────────────────────────┘
       │
       ├─ /help, /reset, /list_agents
       │
       └─────────────────┬──────────────────────────┐
                         │                          │
                         ▼                          ▼
               ┌────────────────────────┐  ┌─────────────────────┐
               │    AgentLoop           │  │  ToolRegistry       │
               │  - Context Building    │  │  - FilesystemTool   │
               │  - LLM Calls           │  │  - ShellTool        │
               │  - Tool Execution      │  │  - WebTool          │
               │  - Session Persistence │  │  - GitHubTool       │
               │  - Hooks (Before/After)│  │  - CronTool         │
               └──────────┬─────────────┘  │  - McpTool          │
                          │                └─────────────────────┘
                          ▼
            ┌─────────────────────────────────┐
            │  Provider Registry              │
            │  ┌─────────────────────────────┐│
            │  │ • Copilot (OAuth)           ││
            │  │ • OpenAI (API Key)          ││
            │  │ • Anthropic (API Key)       ││
            │  │ • Custom Providers          ││
            │  └─────────────────────────────┘│
            └─────────────────────────────────┘
                         │
                         ▼
            ┌────────────────────────────┐
            │  SessionManager            │
            │  (JSONL Persistence)       │
            └────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ Core Services (DI Container)                                 │
│ - IMessageBus, IActivityStream, IBotNexusMetrics             │
│ - ISessionManager, ICommandRouter                            │
│ - IAgentRouter, ChannelManager, ToolRegistry                │
│ - ProviderRegistry, ExtensionLoadReport                      │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ Configuration (appsettings.json → BotNexusConfig)            │
│ - Agents, Providers, Channels, Tools                         │
│ - Gateway (host, port, authentication)                       │
│ - Extensions (loader path, security settings)                │
└──────────────────────────────────────────────────────────────┘
```

---

## 3. Message Flow

### End-to-End Message Processing

```
1. INBOUND (Channel → Message Bus)
   ┌─────────────┐
   │   Channel   │ (Discord, Slack, Telegram, WebSocket, etc.)
   └──────┬──────┘
          │ Receives message from external service
          ▼
   ┌──────────────────────────┐
   │ BaseChannel handler      │
   │ (OnMessageReceived)      │
   └──────┬───────────────────┘
          │ Creates InboundMessage
          │ {from: senderId, content, channel, sessionKey, ...}
          ▼
   ┌──────────────────────────┐
   │ IMessageBus.PublishAsync │ (Bounded channel, capacity 1000)
   └──────┬───────────────────┘
          │
          ▼
   ┌──────────────────────────┐
   │ Message Bus Queue        │
   │ (Async enumerable)       │
   └──────────────────────────┘

2. PROCESSING (Gateway Main Loop)
   ┌──────────────────────────┐
   │ Gateway.ExecuteAsync()   │
   │ (BackgroundService)      │
   └──────┬───────────────────┘
          │ ReadAllAsync from MessageBus
          ▼
   ┌──────────────────────────┐
   │ AgentRouter.RouteAsync   │
   │ - Parse agent metadata   │
   │ - Resolve runner(s)      │
   │ - Broadcast if needed    │
   └──────┬───────────────────┘
          │
          ▼
   ┌──────────────────────────┐
   │ AgentRunner.RunAsync     │
   │ (Per-agent coordinator)  │
   └──────┬───────────────────┘
          │
          ├─ Try CommandRouter first
          │  (for /commands)
          │
          ├─ Or AgentLoop if not command
          │
          ▼
   ┌──────────────────────────┐
   │ CommandRouter            │
   │ /help, /reset, etc.      │
   └──────────────────────────┘
          OR
   ┌──────────────────────────┐
   │ AgentLoop.RunAsync       │
   └──────┬───────────────────┘
          │
          ▼ (11-step cycle)
   ┌──────────────────────────────────────────────────────┐
   │ 1. Get/create session via SessionManager             │
   │ 2. Call IAgentHook.OnBeforeAsync hooks               │
   │ 3. Add user message to session history               │
   │ 4. Register additional tools if provided             │
   │ 5. Loop (max 40 iterations):                         │
   │    a. Build context via ContextBuilder               │
   │       (trim history to fit token window)             │
   │    b. Create ChatRequest with tools                  │
   │    c. Call ILlmProvider.ChatAsync                    │
   │    d. Record metrics                                 │
   │    e. Add response to session history                │
   │    f. If tool calls: execute via ToolRegistry        │
   │    g. Add tool results to history                    │
   │    h. Continue loop if more tools needed             │
   │ 6. Save session to disk                              │
   │ 7. Call IAgentHook.OnAfterAsync hooks                │
   │ 8. Return response to channel                        │
   └──────────────────────────────────────────────────────┘

3. RESPONSE (Outbound)
   ┌──────────────────────────┐
   │ AgentLoop returns        │
   │ OutboundMessage response │
   └──────┬───────────────────┘
          │
          ▼
   ┌──────────────────────────┐
   │ IChannel.SendAsync       │
   │ (Route back to origin)   │
   └──────┬───────────────────┘
          │ Discord, Slack, Telegram, WebSocket, etc.
          ▼
   ┌──────────────────────────┐
   │ External Channel API     │
   │ Message posted/sent      │
   └──────────────────────────┘

4. OBSERVABILITY (Parallel)
   ┌──────────────────────────┐
   │ Gateway publishes        │
   │ ActivityEvent            │
   └──────┬───────────────────┘
          │
          ▼
   ┌──────────────────────────┐
   │ IActivityStream          │
   │ (Event broadcast)        │
   └──────┬───────────────────┘
          │
          ▼
   ┌──────────────────────────┐
   │ WebUI WebSocket clients  │
   │ (Real-time monitoring)   │
   └──────────────────────────┘
```

### Correlation Flow

Each message is tagged with a correlation ID:
- Generated once at channel ingress
- Propagated through all downstream operations
- Attached to logs, metrics, and activity events
- Enables tracing entire request across all services

---

## 4. Dynamic Extension Loading

BotNexus loads extensions **only when explicitly configured**—nothing loads by default. This minimizes attack surface and keeps the deployment minimal.

### Extension Types

Three extension categories are supported:

1. **Providers** — LLM backends (OpenAI, Anthropic, Copilot)
2. **Channels** — Message sources (Discord, Slack, Telegram)
3. **Tools** — Agent capabilities (custom plugins)

### Folder Structure

```
extensions/
├── channels/
│   ├── discord/
│   │   ├── BotNexus.Channels.Discord.dll
│   │   └── dependencies/
│   ├── slack/
│   │   ├── BotNexus.Channels.Slack.dll
│   │   └── dependencies/
│   └── telegram/
│       ├── BotNexus.Channels.Telegram.dll
│       └── dependencies/
├── providers/
│   ├── copilot/
│   │   ├── BotNexus.Providers.Copilot.dll
│   │   └── dependencies/
│   ├── openai/
│   │   ├── BotNexus.Providers.OpenAI.dll
│   │   └── dependencies/
│   └── anthropic/
│       ├── BotNexus.Providers.Anthropic.dll
│       └── dependencies/
└── tools/
    ├── github/
    │   ├── BotNexus.Tools.GitHub.dll
    │   └── dependencies/
    └── custom_tool_name/
        ├── CustomTool.dll
        └── dependencies/
```

### Configuration Example

```json
{
  "BotNexus": {
    "Extensions": {
      "LoadPath": "./extensions",
      "RequireSignedAssemblies": false,
      "Providers": ["copilot", "openai"],
      "Channels": ["discord", "slack"],
      "Tools": ["github"]
    },
    "Providers": {
      "copilot": {
        "Enabled": true,
        "Auth": "oauth"
      },
      "openai": {
        "Enabled": true,
        "ApiKey": "sk-...",
        "Auth": "apikey"
      }
    },
    "Channels": {
      "discord": {
        "Enabled": true,
        "Token": "discord_bot_token"
      },
      "slack": {
        "Enabled": true,
        "AppId": "slack_app_id"
      }
    }
  }
}
```

### Loading Mechanism

**File:** `BotNexus.Core/Extensions/ExtensionLoaderExtensions.cs`

**Process:**

1. **Scan Configuration** — Read enabled extensions from `BotNexusConfig.Extensions`
2. **Load Assemblies** — For each extension:
   - Locate assembly DLL in `extensions/{type}/{name}/`
   - Validate signature (if `RequireSignedAssemblies` enabled)
   - Load via `AssemblyLoadContext` (isolated per extension)
3. **Register Services** — For each loaded assembly:
   - Check for `IExtensionRegistrar` implementation
     - If found: call `Register(services, config)` for full DI control
     - If not found: convention-based discovery
       - Scan for implementations of `IChannel`, `ILlmProvider`, `ITool`
       - Auto-register with sensible defaults
4. **Report Results** — Create `ExtensionLoadReport` with:
   - Success/failure for each extension
   - Any validation errors
   - Loaded type counts

### Registration Patterns

#### Pattern 1: IExtensionRegistrar (Full Control)

```csharp
public class DiscordExtension : IExtensionRegistrar
{
    public void Register(IServiceCollection services, 
                        ProviderConfig config)
    {
        services.AddSingleton<IChannel>(sp =>
            new DiscordChannel(config));
    }
}
```

#### Pattern 2: Convention-Based (Zero Config)

```csharp
public class OpenAiProvider : ILlmProvider
{
    // Auto-discovered and registered by ExtensionLoader
}
```

### AssemblyLoadContext Isolation

Each extension loads in its own `AssemblyLoadContext`:

- **Benefit 1**: Dependency conflicts isolated (extension A can use Newtonsoft 12.0, extension B can use 13.0)
- **Benefit 2**: Future hot-reload capability (unload context without process restart)
- **Benefit 3**: Reduced memory footprint (shared framework types only)

### Security Features

- **Signature Validation**: Optional requirement for signed assemblies
- **Allowed Shared Assemblies**: Whitelist of core types extensions can depend on
- **Max Assemblies Per Extension**: Limit to prevent DOS attacks
- **Dry-Run Mode**: Validate without actually loading

---

## 5. Dependency Injection

BotNexus uses Microsoft.Extensions.DependencyInjection (standard .NET DI container).

### Service Lifetimes

| Service | Lifetime | Reason |
|---------|----------|--------|
| `IMessageBus` | Singleton | Single queue for all messages |
| `IActivityStream` | Singleton | System-wide event broadcast |
| `IBotNexusMetrics` | Singleton | Aggregate metrics |
| `ISessionManager` | Singleton | Thread-safe persistent storage |
| `ProviderRegistry` | Singleton | Provider cache |
| `ChannelManager` | Singleton | Manages all channel lifecycles |
| `IChannel` implementations | Singleton | Long-lived connections |
| `ILlmProvider` implementations | Singleton | Connection pooling |
| `ITool` implementations | Singleton | Stateless tools |
| `Gateway` | Singleton, BackgroundService | Main orchestrator |
| `CronService` | Singleton, BackgroundService | Scheduled jobs |
| `HeartbeatService` | Singleton, BackgroundService | Keep-alives |
| `IAgentHook` implementations | Transient | Fresh per request |
| Per-request objects | Scoped | HTTP request context |

### Core Service Registration

**File:** `BotNexus.Core/Extensions/ServiceCollectionExtensions.cs`

```csharp
public static IServiceCollection AddBotNexusCore(
    this IServiceCollection services, 
    BotNexusConfig config)
{
    services.Configure<BotNexusConfig>(options =>
        // Bind from appsettings.json "BotNexus" section
    );
    
    services.AddSingleton<IMessageBus>(_ =>
        new MessageBus(capacity: 1000));
    
    services.AddSingleton<IActivityStream>(_ =>
        new ActivityStream());
    
    services.AddSingleton<IBotNexusMetrics>(_ =>
        new BotNexusMetrics());
    
    return services;
}
```

### Gateway Service Registration

**File:** `BotNexus.Gateway/BotNexusServiceExtensions.cs`

```csharp
public static IServiceCollection AddBotNexus(
    this IServiceCollection services, 
    BotNexusConfig config)
{
    // Add core services
    services.AddBotNexusCore(config);
    
    // Add extensions (channels, providers, tools)
    services.AddBotNexusExtensions(config);
    
    // Add gateway-specific services
    services.AddSingleton(new ProviderRegistry(providers));
    services.AddSingleton<IAgentRouter, AgentRouter>();
    services.AddSingleton<ChannelManager>();
    services.AddSingleton<CronService>();
    services.AddSingleton<HeartbeatService>();
    
    // Add Gateway as BackgroundService
    services.AddHostedService<Gateway>();
    
    // Add health checks
    services.AddHealthChecks()
        .AddCheck<MessageBusHealthCheck>("message_bus")
        .AddCheck<ProviderRegistryHealthCheck>("provider_registration")
        .AddCheck<ExtensionLoaderHealthCheck>("extension_loader")
        .AddReadinessCheck<ChannelReadinessCheck>("channel_readiness")
        .AddReadinessCheck<ProviderReadinessCheck>("provider_readiness");
    
    return services;
}
```

### Extension Service Registration

**File:** `BotNexus.Core/Extensions/ExtensionLoaderExtensions.cs`

```csharp
private static void AddBotNexusExtensions(
    this IServiceCollection services, 
    BotNexusConfig config)
{
    var loader = new ExtensionLoader(config);
    var report = loader.LoadExtensions(services);
    
    services.AddSingleton(report);
    
    // Convention-based or IExtensionRegistrar-based
    // registration happens inside loader
}
```

---

## 6. Core Abstractions

The Core module defines 13 interfaces that form the contract between the engine and all extensions:

| Interface | Location | Purpose |
|-----------|----------|---------|
| `IMessageBus` | `Abstractions/IMessageBus.cs` | Async message queue between channels and agents |
| `IChannel` | `Abstractions/IChannel.cs` | Messaging channel contract (Discord, Slack, etc.) |
| `IAgentRunner` | `Abstractions/IAgentRunner.cs` | Per-agent message processor |
| `ILlmProvider` | `Abstractions/ILlmProvider.cs` | LLM backend contract |
| `ITool` | `Abstractions/ITool.cs` | Executable tool callable by agents |
| `ICommandRouter` | `Abstractions/ICommandRouter.cs` | Routes /commands (not agent messages) |
| `ISessionManager` | `Abstractions/ISessionManager.cs` | Persistent conversation storage |
| `IExtensionRegistrar` | `Abstractions/IExtensionRegistrar.cs` | Optional DI registration hook for extensions |
| `IOAuthProvider` | `Abstractions/IOAuthProvider.cs` | OAuth token acquisition and validation |
| `IOAuthTokenStore` | `Abstractions/IOAuthTokenStore.cs` | Persistent OAuth token storage |
| `IActivityStream` | `Abstractions/IActivityStream.cs` | System-wide event publication |
| `IAgentHook` | `Abstractions/IAgentHook.cs` | Pipeline middleware (before/after/error) |
| `IMemoryStore` | `Abstractions/IMemoryStore.cs` | Persistent agent memory/notes |

### Key Design Principles

- **Small Surface**: Each interface ≤ 5 methods
- **Focused**: One responsibility per interface
- **Extendable**: All implemented outside Core
- **Async-First**: All I/O operations return `Task` or `ValueTask`
- **Testable**: No static dependencies, everything injected

---

## 7. Multi-Agent Routing

BotNexus supports multiple agents running concurrently, each with independent configurations.

### Agent Router

**File:** `BotNexus.Gateway/AgentRouter.cs`

The `AgentRouter` resolves which agent(s) should handle an inbound message:

```
InboundMessage
    ├─ Metadata:
    │  ├─ "agent" (exact name)
    │  ├─ "agent_name" (exact name)
    │  └─ "agentName" (camelCase)
    │
    ├─ If agent name specified
    │  └─ Route to that agent's runner
    │
    ├─ If broadcast token ("all", "*")
    │  └─ Route to all agent runners
    │
    └─ If unspecified
       ├─ If GatewayConfig.DefaultAgent set
       │  └─ Route to default
       └─ Else if BroadcastWhenAgentUnspecified
          └─ Route to all
       └─ Else error
```

### Agent Configuration

**File:** `BotNexus.Core/Configuration/AgentConfig.cs`

Each agent is independently configured:

```json
{
  "BotNexus": {
    "Agents": {
      "default": {
        "Name": "default",
        "SystemPrompt": "You are a helpful assistant",
        "Model": "gpt-4o",
        "Provider": "openai",
        "MaxTokens": 2000,
        "Temperature": 0.7,
        "MaxToolIterations": 40,
        "Timezone": "UTC",
        "EnableMemory": true,
        "McpServers": [
          {
            "Name": "filesystem",
            "Command": "mcp-filesystem"
          }
        ],
        "CronJobs": [
          {
            "Name": "daily_digest",
            "Schedule": "0 8 * * *",
            "Prompt": "Generate daily digest"
          }
        ]
      },
      "planner": {
        "Name": "planner",
        "SystemPrompt": "You are a project planner",
        "Model": "gpt-4o",
        "Provider": "openai",
        "MaxToolIterations": 50
      }
    }
  }
}
```

### Agent Runner

**File:** `BotNexus.Agent/AgentRunner.cs`

One `AgentRunner` per configured agent:

```csharp
public class AgentRunner : IAgentRunner
{
    public async Task RunAsync(InboundMessage message)
    {
        // 1. Try command router first
        if (await _commandRouter.TryHandleAsync(message))
            return; // Command handled
        
        // 2. Run agent loop
        var response = await _agentLoop.RunAsync(message);
        
        // 3. Send response through original channel
        await message.Channel.SendAsync(response);
    }
}
```

---

## 8. Provider Architecture

### ILlmProvider Interface

**File:** `BotNexus.Core/Abstractions/ILlmProvider.cs`

```csharp
public interface ILlmProvider
{
    string DefaultModel { get; }
    GenerationSettings Generation { get; }
    Task<ChatResponse> ChatAsync(ChatRequest request);
    Task<IAsyncEnumerable<StreamedChatDelta>> ChatStreamAsync(ChatRequest request);
}
```

### LlmProviderBase (Abstract Base)

**File:** `BotNexus.Providers.Base/LlmProviderBase.cs`

Provides common infrastructure:

- **Retry Logic**: Exponential backoff (configurable)
- **Error Handling**: Transient vs. permanent errors
- **Metrics**: Track latency and call counts
- **Streaming**: SSE handling for streaming responses

```csharp
public abstract class LlmProviderBase : ILlmProvider
{
    public async Task<ChatResponse> ChatAsync(ChatRequest request)
    {
        return await RetryAsync(
            () => ChatCoreAsync(request),
            maxRetries: Generation.MaxRetries,
            backoff: Generation.RetryBackoff
        );
    }
    
    protected abstract Task<ChatResponse> ChatCoreAsync(
        ChatRequest request);
}
```

### Provider Implementations

#### Copilot Provider (OAuth)

**File:** `BotNexus.Providers.Copilot/CopilotProvider.cs`

- **Auth**: GitHub OAuth device code flow
- **Endpoint**: `https://api.githubcopilot.com`
- **API Format**: OpenAI-compatible
- **Default Model**: Latest Copilot model

```
Device Code Flow:
1. Request device code from GitHub
2. Display code to user (e.g., "Enter code: ABCD-1234")
3. Poll GitHub while user authorizes
4. Receive access token on approval
5. Cache token locally (encrypted)
6. Use token for API calls
```

#### OpenAI Provider (API Key)

**File:** `BotNexus.Providers.OpenAI/OpenAiProvider.cs`

- **Auth**: API key authentication
- **Endpoint**: `https://api.openai.com/v1` (or custom via `apiBase`)
- **API Format**: Official OpenAI Chat Completions API
- **Default Model**: `gpt-4o`
- **Tooling**: Full support for tool calling

#### Anthropic Provider (API Key)

**File:** `BotNexus.Providers.Anthropic/AnthropicProvider.cs`

- **Auth**: API key authentication
- **Endpoint**: `https://api.anthropic.com`
- **API Format**: Anthropic Messages API (converted to/from ChatRequest)
- **Default Model**: `claude-3-5-sonnet-20241022`
- **Tooling**: Partial support (no streaming tool use)

### Provider Registry

**File:** `BotNexus.Providers.Base/ProviderRegistry.cs`

The provider registry maintains a map of available providers and resolves which provider should handle a request:

```csharp
public class ProviderRegistry
{
    public ILlmProvider Get(string providerName);
    public ILlmProvider GetDefault();
    public IEnumerable<string> GetProviderNames();
}
```

### Provider Resolution Strategy (from AgentLoop)

When processing a request, the agent selects a provider via this priority:

1. **Explicit Configuration**: `AgentConfig.Provider` name
2. **Model Prefix**: `"provider:model"` (e.g., `"anthropic:claude-3"`)
3. **Default Model Match**: Model name matches provider's `DefaultModel`
4. **Registry Default**: First registered provider
5. **Error**: No provider available

Example:

```csharp
// All of these resolve to OpenAI provider:
"openai:gpt-4o"      // Explicit prefix
"gpt-4o"             // Matches OpenAI's DefaultModel
```

---

## 9. Session Management

Conversations are persisted to disk in a structured format, enabling session recovery and history inspection.

### SessionManager

**File:** `BotNexus.Session/SessionManager.cs`

- **Storage**: File-backed JSONL (one file per session)
- **Location**: Configurable path (default: `./sessions`)
- **Thread-Safety**: Per-session `SemaphoreSlim` lock
- **Caching**: In-memory cache with weak references
- **Key Encoding**: URI escaping (`%` → `_`) to sanitize filesystem paths

### Session Model

**File:** `BotNexus.Core/Models/Session.cs`

```csharp
public class Session
{
    public string Key { get; set; }           // Unique identifier
    public string AgentName { get; set; }    // Which agent
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<SessionEntry> History { get; set; }
}

public class SessionEntry
{
    public MessageRole Role { get; set; }     // User, Assistant, Tool, System
    public string Content { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? ToolName { get; set; }     // If tool call
    public string? ToolCallId { get; set; }
}
```

### Session Key Format

Default format: `{Channel}:{ChatId}`

Example: `discord:12345` → Conversation between Discord user 12345 and the agent

Can be overridden via `InboundMessage.SessionKeyOverride` for custom session grouping.

### File Layout

```
sessions/
├── discord_12345.jsonl
├── slack_U123ABC.jsonl
├── telegram_999.jsonl
├── websocket_abc123_history.jsonl
└── custom_session_key.jsonl
```

### JSONL Format

Each line is a `SessionEntry` JSON object:

```jsonl
{"role":"System","content":"You are a helpful assistant","timestamp":"2026-04-01T10:00:00Z"}
{"role":"User","content":"What is 2+2?","timestamp":"2026-04-01T10:00:05Z"}
{"role":"Assistant","content":"2+2 equals 4","timestamp":"2026-04-01T10:00:06Z"}
{"role":"Tool","content":"{\"result\":true}","toolName":"Calculator","toolCallId":"call_123","timestamp":"2026-04-01T10:00:06Z"}
```

---

## 10. Security Model

### Authentication

#### API Key Authentication (Channels & REST)

- **Mechanism**: `Authorization: Bearer {api_key}` header
- **Validation**: Middleware intercepts requests to `/api/*` and `/ws`
- **Configuration**: `GatewayConfig.ApiKey`
- **Scope**: Global—single key for entire Gateway

#### OAuth Authentication (Providers)

- **Mechanism**: GitHub device code flow (for Copilot)
- **Flow**:
  1. Request device code
  2. User authorizes on GitHub
  3. Exchange device code for access token
  4. Cache token locally (encrypted)
- **Scope**: Per-provider (each provider can use OAuth independently)

### Authorization

Currently not implemented. Future consideration:

- RBAC (role-based access control)
- ABAC (attribute-based access control)
- Per-channel permissions
- Per-agent permissions

### Extension Security

- **Signature Validation**: Optional requirement that extensions be signed
- **Sandboxing**: AssemblyLoadContext isolation reduces attack surface
- **Validation**: Pre-load dry-run to catch errors before live deployment
- **Whitelist**: Allowed shared assemblies list prevents dependency conflicts

### Data Security

- **Session Files**: Stored on disk in plaintext (configure file permissions)
- **OAuth Tokens**: Encrypted at rest (file-based store)
- **Logs**: Configurable log levels (prevent credential leakage)
- **Connections**: Configure TLS for WebSocket/REST via Kestrel

### API Key Security

- **Never log full keys**: Log last 4 characters only
- **Rotate regularly**: Manual process (update appsettings.json + restart)
- **Secrets management**: Use OS environment variables or secrets manager

---

## 11. Observability

BotNexus provides multiple observability mechanisms to monitor health and behavior.

### Health Checks

**Endpoint**: `GET /health` (liveness), `GET /ready` (readiness)

| Check | Type | Purpose |
|-------|------|---------|
| `message_bus` | Liveness | IMessageBus alive |
| `provider_registration` | Liveness | At least one provider registered |
| `extension_loader` | Liveness | Extension load report available |
| `channel_readiness` | Readiness | All configured channels running |
| `provider_readiness` | Readiness | At least one provider ready |

### Metrics

**File:** `BotNexus.Core/Metrics/BotNexusMetrics.cs`

- **Provider Latency**: Per-provider response time histogram
- **Tool Execution**: Per-tool duration and success/failure rates
- **Message Throughput**: Messages per channel, per second
- **Session Counts**: Total sessions, active sessions
- **Error Rates**: Exceptions by type

### Correlation IDs

Every message receives a unique correlation ID at channel ingress:

- Propagated through Gateway, AgentRouter, AgentRunner, AgentLoop
- Attached to all logs and metrics
- Returned in responses for end-to-end tracing

### Activity Stream

**File:** `BotNexus.Core/Bus/ActivityStream.cs`

System-wide event broadcast for real-time WebUI monitoring:

```csharp
public class ActivityEvent
{
    public ActivityEventType Type { get; set; }
    public string Channel { get; set; }
    public string SessionId { get; set; }
    public string ChatId { get; set; }
    public string SenderId { get; set; }
    public string Content { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public enum ActivityEventType
{
    MessageReceived,
    MessageSent,
    ToolExecuted,
    Error,
    SessionCreated,
    SessionReset
}
```

### Logging

- **Levels**: Debug, Information, Warning, Error, Critical
- **Scopes**: Correlation ID, Channel, SessionId
- **Targets**: Console (development), Event Log (production), Serilog (optional)

---

## 12. Installation Layout

### Runtime Directory Structure

BotNexus installs under `~/.botnexus/` (user home directory):

```
~/.botnexus/
├── config/
│   ├── appsettings.json         # Main configuration
│   ├── appsettings.prod.json    # Production overrides
│   └── agents/
│       └── system_prompts/      # Agent prompts
├── sessions/                    # Persistent conversation data
│   ├── discord_12345.jsonl
│   ├── slack_U123ABC.jsonl
│   └── ...
├── tokens/                      # OAuth token storage (encrypted)
│   └── oauth_tokens.json
├── extensions/                  # Dynamic extensions
│   ├── providers/
│   │   ├── copilot/
│   │   ├── openai/
│   │   └── anthropic/
│   ├── channels/
│   │   ├── discord/
│   │   ├── slack/
│   │   └── telegram/
│   └── tools/
│       └── github/
├── logs/                        # Log files
│   ├── botnexus.log
│   ├── botnexus.error.log
│   └── ...
└── cache/                       # Transient cache
    ├── web_fetch/              # Cached web content
    └── ...
```

### Configuration Resolution

Startup loads configuration in this order:

1. `appsettings.json` (base)
2. `appsettings.{ASPNETCORE_ENV}.json` (environment-specific overrides)
3. Environment variables (highest priority)

### First-Run Setup

On first run:

1. Create `~/.botnexus/` directory
2. Generate default `appsettings.json`
3. Scan `extensions/` folder
4. Initialize extension registry
5. Set up session storage
6. Create OAuth token store (if needed)

---

## 13. Component Reference

### Class Hierarchy

```
BotNexus.Core/
├── Abstractions/          (13 interfaces)
├── Bus/
│   ├── MessageBus.cs      (IMessageBus)
│   └── ActivityStream.cs  (IActivityStream)
├── Configuration/
│   ├── BotNexusConfig.cs
│   ├── AgentConfig.cs
│   └── ...
├── Models/
│   ├── InboundMessage.cs
│   ├── OutboundMessage.cs
│   ├── Session.cs
│   ├── ChatRequest.cs
│   ├── ChatResponse.cs
│   └── ...
├── Extensions/
│   ├── ServiceCollectionExtensions.cs
│   └── ExtensionLoaderExtensions.cs
└── Metrics/
    └── BotNexusMetrics.cs

BotNexus.Gateway/
├── Program.cs
├── BotNexusServiceExtensions.cs
├── Gateway.cs             (Main orchestrator)
├── AgentRouter.cs
├── ChannelManager.cs
├── WebSocketChannel.cs
├── GatewayWebSocketHandler.cs
└── Health/
    ├── MessageBusHealthCheck.cs
    └── ...

BotNexus.Agent/
├── AgentLoop.cs           (Core processing)
├── AgentRunner.cs
├── ContextBuilder.cs
├── Tools/
│   ├── ToolBase.cs
│   ├── ToolRegistry.cs
│   ├── FilesystemTool.cs
│   ├── ShellTool.cs
│   └── ...
└── Mcp/
    ├── McpTool.cs
    └── IMcpClient.cs

BotNexus.Channels.Base/
├── BaseChannel.cs
├── ChannelManager.cs
└── Models/
    └── ...

BotNexus.Channels.{Discord,Slack,Telegram}/
├── {Provider}Channel.cs

BotNexus.Providers.Base/
├── LlmProviderBase.cs
├── ProviderRegistry.cs
└── Models/
    └── ...

BotNexus.Providers.{Copilot,OpenAI,Anthropic}/
├── {Provider}Provider.cs
└── ...

BotNexus.Session/
└── SessionManager.cs
```

### Key File Locations

| Purpose | File |
|---------|------|
| Solution root | `BotNexus.slnx` |
| Gateway entry point | `src/BotNexus.Gateway/Program.cs` |
| Core contracts | `src/BotNexus.Core/Abstractions/` |
| DI setup | `src/BotNexus.Gateway/BotNexusServiceExtensions.cs` |
| Message processing | `src/BotNexus.Gateway/Gateway.cs` |
| Agent execution | `src/BotNexus.Agent/AgentLoop.cs` |
| Session storage | `src/BotNexus.Session/SessionManager.cs` |
| Extension loading | `src/BotNexus.Core/Extensions/ExtensionLoaderExtensions.cs` |
| WebUI | `src/BotNexus.WebUI/wwwroot/` |
| Tests | `tests/` |

---

## Summary

BotNexus is a modular, contract-first platform for running multiple AI agents concurrently. It emphasizes:

- **Extensibility**: Dynamic assembly loading with folder-based organization
- **Security**: Extension validation, OAuth support, configuration-driven loading
- **Observability**: Correlation IDs, health checks, activity stream
- **Resilience**: Retry logic, error handling, session persistence
- **Maintainability**: Clean separation of concerns, minimal core, SOLID principles

The architecture supports deploying to `~/.botnexus/` with configurable extensions, persistent sessions, and OAuth-backed providers.
