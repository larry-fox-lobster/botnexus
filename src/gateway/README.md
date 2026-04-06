# BotNexus Gateway

The **Gateway** is the central orchestrator of the BotNexus platform. It manages multi-agent execution, session persistence, real-time WebSocket streaming, and extensibility via pluggable strategies and channel adapters.

## Architecture

The Gateway is composed of **5 core projects**:

| Project | Purpose |
|---------|---------|
| **BotNexus.Gateway.Abstractions** | Interface contracts (IAgentConfigurationSource, IIsolationStrategy, IChannelAdapter, ISessionStore) |
| **BotNexus.Gateway** | Main host — message routing, agent supervision, hot reload, channel management |
| **BotNexus.Gateway.Api** | REST API (agents, sessions, chat) + WebSocket handler + WebUI static files |
| **BotNexus.Gateway.Sessions** | Session persistence implementations (InMemory, FileSessionStore) |
| **BotNexus.Gateway.WebUI** | Real-time monitoring dashboard (React/TypeScript) |

### Message Flow

```
External Channel (Slack, Discord, etc.)
         ↓
    IChannelAdapter
         ↓
  ┌─────────────────────────┐
  │   BotNexus.Gateway      │
  │  [Message Router]       │  ← Hot reload watches config
  └──────────┬──────────────┘
             ↓
    IAgentConfigurationSource
    (PlatformConfigLoader)
             ↓
   ┌─────────────────────────┐
   │  IAgentSupervisor       │
   │ [Agent Registry & Pool] │
   └──────────┬──────────────┘
              ↓
   IIsolationStrategy
   (in-process/sandbox/container)
              ↓
   Agent executes with LLM provider
              ↓
   ISessionStore
   (Persists conversation history)
```

## Extension Points

The Gateway is **fully extensible** through pluggable interfaces:

### 1. IIsolationStrategy
Controls how agent code is executed and isolated.

```csharp
public interface IIsolationStrategy
{
    string Name { get; }
    Task<IAgentHandle> CreateAsync(
        AgentDescriptor descriptor,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default);
}
```

**Built-in strategies:**
- **in-process** — Runs agent directly in Gateway process (default, fastest)
- **sandbox** — Restricted AppDomain or process with limited permissions (Phase 2)
- **container** — Docker container isolation (Phase 2)
- **remote** — HTTP/gRPC delegation to remote service (Phase 2)

**To add a custom strategy:**
1. Implement `IIsolationStrategy`
2. Register via dependency injection in `Program.cs`
3. Set `isolationStrategy: "your-strategy"` in agent config

### 2. IChannelAdapter
Connects external communication platforms (Slack, Discord, Telegram, etc.) to the Gateway.

```csharp
public interface IChannelAdapter
{
    string ChannelType { get; }      // "slack", "discord", "telegram"
    string DisplayName { get; }
    bool SupportsStreaming { get; }
    bool IsRunning { get; }
    
    Task StartAsync(IChannelDispatcher dispatcher, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task SendAsync(OutboundMessage message, CancellationToken ct = default);
    Task SendStreamDeltaAsync(string conversationId, string delta, CancellationToken ct = default);
}
```

**To add a custom channel:**
1. Implement `IChannelAdapter` with your protocol (e.g., Slack Events API)
2. Register in `Program.cs` and add to dependency injection
3. Enable in `config.json`:
   ```json
   {
     "channels": {
       "my-platform": {
         "type": "my-platform",
         "enabled": true,
         "settings": {
           "token": "your-secret",
           "workspace": "your-workspace"
         }
       }
     }
   }
   ```

### 3. ISessionStore
Persists conversation history. Swap implementations for different backends.

```csharp
public interface ISessionStore
{
    Task<GatewaySession?> GetAsync(string sessionId, CancellationToken ct = default);
    Task<GatewaySession> GetOrCreateAsync(string sessionId, string agentId, CancellationToken ct = default);
    Task SaveAsync(GatewaySession session, CancellationToken ct = default);
    Task DeleteAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<GatewaySession>> ListAsync(string? agentId = null, CancellationToken ct = default);
}
```

**Built-in implementations:**
- **InMemorySessionStore** — Fast, non-durable (development/testing)
- **FileSessionStore** — JSONL files with `.meta.json` sidecar

**To use a database backend:**
1. Implement `ISessionStore` (e.g., `SqlSessionStore`)
2. Register in `Program.cs`:
   ```csharp
   builder.Services.AddSingleton<ISessionStore, SqlSessionStore>();
   ```

### 4. IAgentConfigurationSource
Loads and watches agent definitions from external sources.

```csharp
public interface IAgentConfigurationSource
{
    Task<IReadOnlyList<AgentDescriptor>> LoadAsync(CancellationToken ct = default);
    IDisposable? Watch(Action<IReadOnlyList<AgentDescriptor>> onChanged);
}
```

**Built-in implementation:**
- **PlatformConfigLoader** — Loads from `~/.botnexus/config.json` with file watching for hot reload

**To add a dynamic config source (e.g., Azure AppConfig):**
1. Implement `IAgentConfigurationSource`
2. Register in `Program.cs`
3. Implement `Watch()` to notify on config changes

## Configuration

### PlatformConfig (~/.botnexus/config.json)

The Gateway reads from `~/.botnexus/config.json`. Configure via nested `gateway` section:

```json
{
  "gateway": {
    "listenUrl": "http://localhost:5005",
    "defaultAgentId": "assistant",
    "sessionsDirectory": "workspace/sessions",
    "logLevel": "Information",
    "apiKeys": {
      "user-key-1": {
        "apiKey": "sk-...",
        "tenantId": "tenant-1",
        "displayName": "User #1 API Key",
        "allowedAgents": ["assistant"],
        "permissions": ["chat:send", "sessions:read"],
        "isAdmin": false
      }
    }
  },
  "agents": {
    "assistant": {
      "provider": "copilot",
      "model": "gpt-4.1",
      "systemPromptFile": "prompts/assistant.txt",
      "isolationStrategy": "in-process",
      "enabled": true
    }
  },
  "providers": {
    "copilot": {
      "apiKey": "copilot",
      "baseUrl": "https://api.githubcopilot.com",
      "defaultModel": "gpt-4.1"
    }
  },
  "channels": {
    "discord": {
      "type": "discord",
      "enabled": true,
      "settings": {
        "token": "discord-bot-token"
      }
    }
  }
}
```

### Environment Variable Overrides

Use `BotNexus__Path__To__Property` format:

```bash
export BotNexus__Gateway__ListenUrl="http://0.0.0.0:8080"
export BotNexus__Gateway__DefaultAgentId="researcher"
export BotNexus__LogLevel="Debug"
```

### Legacy Root-Level Form (Deprecated)

For backward compatibility, Gateway settings can also be at the root:

```json
{
  "listenUrl": "http://localhost:5005",
  "defaultAgentId": "assistant",
  "sessionsDirectory": "workspace/sessions"
}
```

The Gateway prefers the nested `gateway` section if both exist.

## Authentication

The Gateway provides **optional API key authentication** via the `GatewayAuthManager`.

### Resolution Order (for Provider API Keys)

1. **auth.json** — `~/.botnexus/auth.json` (OAuth tokens, enterprise endpoints)
2. **Environment Variables** — `BOTNEXUS_COPILOT_APIKEY`, `BOTNEXUS_OPENAI_APIKEY`, etc.
3. **Platform Config** — `config.json` provider section (`apiKey` field)

### Example: Copilot OAuth

Store in `~/.botnexus/auth.json`:
```json
{
  "copilot": {
    "type": "oauth",
    "access": "ghu_...",
    "refresh": "ghr_...",
    "expires": 1234567890000,
    "endpoint": "https://api.githubcopilot.com"
  }
}
```

Then in `config.json`:
```json
{
  "providers": {
    "copilot": {
      "apiKey": "auth:copilot",  // Reference to auth.json entry
      "baseUrl": "https://api.githubcopilot.com"
    }
  }
}
```

The `GatewayAuthManager` auto-refreshes expired OAuth tokens when needed.

### API Key Validation (Optional)

Enable per-request API key validation by setting `apiKeys` in config:

```json
{
  "gateway": {
    "apiKeys": {
      "key-1": {
        "apiKey": "sk-...",
        "allowedAgents": ["assistant"],
        "permissions": ["chat:send"],
        "isAdmin": false
      }
    }
  }
}
```

## API Endpoints

### Health
- **GET `/health`** — Returns `{ "status": "ok" }`

### Agents
- **GET `/api/agents`** — List all registered agents
  ```json
  [
    {
      "agentId": "assistant",
      "provider": "copilot",
      "model": "gpt-4.1"
    }
  ]
  ```
- **GET `/api/agents/{agentId}`** — Get agent details
- **POST `/api/agents`** — Register a new agent
- **DELETE `/api/agents/{agentId}`** — Unregister an agent
- **GET `/api/agents/instances`** — List active agent instances
- **GET `/api/agents/{agentId}/sessions/{sessionId}/status`** — Check instance status
- **POST `/api/agents/{agentId}/sessions/{sessionId}/stop`** — Stop an instance

### Chat (REST, non-streaming)
- **POST `/api/chat`**
  ```json
  {
    "agentId": "assistant",
    "message": "What is the weather?",
    "sessionId": "optional-session-id"
  }
  ```
  Response:
  ```json
  {
    "sessionId": "...",
    "content": "The weather...",
    "usage": {
      "inputTokens": 50,
      "outputTokens": 100
    }
  }
  ```
- **POST `/api/chat/steer`** — Inject steering message into active agent run
- **POST `/api/chat/follow-up`** — Queue follow-up for next run

### Sessions
- **GET `/api/sessions`** — List all sessions
  - Query param: `?agentId=assistant` (optional filter)
- **GET `/api/sessions/{sessionId}`** — Get session history
  ```json
  {
    "sessionId": "...",
    "agentId": "assistant",
    "entries": [
      { "role": "user", "content": "..." },
      { "role": "assistant", "content": "..." }
    ]
  }
  ```
- **DELETE `/api/sessions/{sessionId}`** — Delete a session

### WebSocket (Real-time Streaming)
- **GET/WebSocket `/ws`**
  - Query params: `?agent={agentId}&session={sessionId}`
  - See [WebSocket Protocol](#websocket-protocol) section in root README

### WebUI
- **GET `/webui`** — Real-time chat dashboard

## WebSocket Protocol

### Connection
```
ws://localhost:5005/ws?agent=assistant&session={sessionId}
```

If `session` is omitted, a new session ID is auto-generated.

### Client → Server Messages

**Send a message:**
```json
{ "type": "message", "content": "What is 2+2?" }
```

**Abort active execution:**
```json
{ "type": "abort" }
```

**Inject steering message into active run:**
```json
{ "type": "steer", "content": "Focus on the main point." }
```

**Queue a follow-up for the next run:**
```json
{ "type": "follow_up", "content": "And what about 3+3?" }
```

**Keepalive ping:**
```json
{ "type": "ping" }
```

### Server → Client Messages

**Connection established:**
```json
{ "type": "connected", "connectionId": "...", "sessionId": "..." }
```

**Agent started processing:**
```json
{ "type": "message_start", "messageId": "uuid-..." }
```

**Thinking delta (streaming agent reasoning):**
```json
{ "type": "thinking_delta", "delta": "Let me think about...", "messageId": "uuid-..." }
```

**Content delta (streaming response text):**
```json
{ "type": "content_delta", "delta": "2+2 is", "messageId": "uuid-..." }
```

**Tool execution started:**
```json
{
  "type": "tool_start",
  "toolCallId": "call_...",
  "toolName": "calculate",
  "messageId": "uuid-..."
}
```

**Tool result received:**
```json
{
  "type": "tool_end",
  "toolCallId": "call_...",
  "toolResult": "4",
  "messageId": "uuid-..."
}
```

**Agent completed:**
```json
{
  "type": "message_end",
  "messageId": "uuid-...",
  "usage": {
    "inputTokens": 50,
    "outputTokens": 100
  }
}
```

**Error occurred:**
```json
{
  "type": "error",
  "message": "Agent not found",
  "code": "NOT_FOUND"
}
```

**Keepalive pong:**
```json
{ "type": "pong" }
```

## Development Quick Start

### Prerequisites
- .NET 10 SDK
- PowerShell (Windows) or bash (Linux/macOS)

### Build the Gateway
```bash
dotnet build src/gateway/BotNexus.Gateway.Api/BotNexus.Gateway.Api.csproj
```

### Run the Dev Server
```bash
# Option 1: PowerShell script (Windows)
.\scripts\start-gateway.ps1

# Option 2: Direct dotnet
dotnet run --project src/gateway/BotNexus.Gateway.Api

# Option 3: With custom port
.\scripts\start-gateway.ps1 -Port 8080
```

The Gateway starts on `http://localhost:5005` by default. Access the WebUI at `http://localhost:5005/webui`.

### Create a Test Agent
Edit `~/.botnexus/config.json`:
```json
{
  "gateway": {
    "listenUrl": "http://localhost:5005",
    "defaultAgentId": "test-agent"
  },
  "agents": {
    "test-agent": {
      "provider": "copilot",
      "model": "gpt-4.1",
      "isolationStrategy": "in-process",
      "enabled": true
    }
  },
  "providers": {
    "copilot": {
      "apiKey": "your-api-key",
      "baseUrl": "https://api.githubcopilot.com"
    }
  }
}
```

### Test via REST
```bash
# Send a message
curl -X POST http://localhost:5005/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "test-agent",
    "message": "Hello, what is your name?"
  }'
```

### Test via WebSocket
```bash
# Use websocat, wscat, or any WebSocket client
wscat -c "ws://localhost:5005/ws?agent=test-agent&session=test-session-1"
# Type: { "type": "message", "content": "Hello!" }
```

### Run Tests
```bash
dotnet test tests/BotNexus.Gateway.Api.Tests/BotNexus.Gateway.Api.Tests.csproj
```

## File Structure

```
src/gateway/
├── BotNexus.Gateway.Abstractions/
│   ├── Agents/
│   │   ├── IAgentConfigurationSource.cs
│   │   ├── IAgentRegistry.cs
│   │   ├── IAgentSupervisor.cs
│   │   └── ...
│   ├── Channels/
│   │   ├── IChannelAdapter.cs
│   │   └── IChannelManager.cs
│   ├── Isolation/
│   │   └── IIsolationStrategy.cs
│   ├── Sessions/
│   │   └── ISessionStore.cs
│   ├── Models/
│   │   ├── AgentDescriptor.cs
│   │   ├── GatewaySession.cs
│   │   └── ...
│   └── ...
├── BotNexus.Gateway/
│   ├── Configuration/
│   │   ├── PlatformConfig.cs
│   │   ├── PlatformConfigLoader.cs
│   │   └── GatewayAuthManager.cs
│   ├── Agents/
│   │   ├── AgentSupervisor.cs
│   │   └── AgentRegistry.cs
│   ├── Channels/
│   │   └── ChannelManager.cs
│   └── ...
├── BotNexus.Gateway.Api/
│   ├── Program.cs
│   ├── Controllers/
│   │   ├── ChatController.cs
│   │   ├── AgentsController.cs
│   │   ├── SessionsController.cs
│   │   └── ConfigController.cs
│   ├── WebSocket/
│   │   └── GatewayWebSocketHandler.cs
│   └── wwwroot/
│       ├── index.html
│       ├── app.js
│       └── ...
├── BotNexus.Gateway.Sessions/
│   ├── InMemorySessionStore.cs
│   ├── FileSessionStore.cs
│   └── ...
└── README.md (this file)
```

## Troubleshooting

### Gateway won't start
1. Check `~/.botnexus/config.json` is valid JSON
2. Verify providers have valid credentials (check `~/.botnexus/auth.json`)
3. Check the port is not in use: `netstat -an | grep 5005`

### Agent not responding
1. Confirm agent is registered: `curl http://localhost:5005/api/agents`
2. Check provider credentials: `curl http://localhost:5005/api/config/providers`
3. Verify LLM provider is accessible (check firewall, VPN, etc.)

### Session history lost
1. Verify `sessionsDirectory` is writable
2. Check disk space availability
3. Confirm `ISessionStore` is not in-memory for production

### WebSocket connection drops
1. Check browser WebSocket support (console for errors)
2. Verify firewall allows WebSocket connections
3. Check Gateway logs for timeout or protocol errors

## Further Reading

- [BotNexus Architecture Overview](../../docs/architecture.md)
- [Configuration Guide](../../docs/configuration.md)
- [Extension Development](../../docs/extension-development.md)
- [API Reference](../../docs/api-reference.md)
