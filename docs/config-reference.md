# Configuration Reference

> Complete reference for `~/.botnexus/config.json` and related configuration files.

## Table of Contents

1. [Config File Location](#config-file-location)
2. [Config Resolution Order](#config-resolution-order)
3. [Schema Version](#schema-version)
4. [Root Fields](#root-fields)
5. [Gateway Settings (`gateway`)](#gateway-settings-gateway)
6. [Agents (`agents`)](#agents-agents)
7. [Providers (`providers`)](#providers-providers)
8. [Channels (`channels`)](#channels-channels)
9. [Extensions (`extensions`)](#extensions-extensions)
10. [API Keys (`apiKeys`)](#api-keys-apikeys)
11. [Session Store (`sessionStore`)](#session-store-sessionstore)
12. [CORS (`cors`)](#cors-cors)
13. [Rate Limiting (`rateLimit`)](#rate-limiting-ratelimit)
14. [Auth File (`auth.json`)](#auth-file-authjson)
15. [Full Annotated Example](#full-annotated-example)
16. [JSON Schema Validation](#json-schema-validation)

---

## Config File Location

The primary configuration file is `config.json` inside the BotNexus home directory.

**Default path:** `~/.botnexus/config.json`

On first run, `BotNexusHome.Initialize()` creates the following directory structure:

```
~/.botnexus/
├── config.json          # Platform configuration
├── auth.json            # Provider authentication credentials
├── extensions/          # Dynamic extension assemblies
├── tokens/              # OAuth token storage
├── sessions/            # Conversation history
├── logs/                # Application logs
└── agents/              # Per-agent workspace directories
    └── {agent-name}/
        ├── SOUL.md
        ├── IDENTITY.md
        ├── USER.md
        └── MEMORY.md
```

---

## Config Resolution Order

The Gateway resolves the config file path using this priority order (highest first):

| Priority | Source | Description |
|----------|--------|-------------|
| 1 | Explicit path | `PlatformConfigLoader.Load(configPath)` parameter or `BotNexus__ConfigPath` env var |
| 2 | `BOTNEXUS_HOME` env var | `$BOTNEXUS_HOME/config.json` |
| 3 | Default home | `~/.botnexus/config.json` (user profile directory) |

If the resolved path does not exist, an empty `PlatformConfig` with default values is returned — the Gateway starts successfully without a config file.

### Home Directory Resolution

The `BotNexusHome` class resolves the home path:

```
BotNexusHome(homePath) →
  1. If homePath provided → use it
  2. Else if BOTNEXUS_HOME env var set → use it
  3. Else → ~/.botnexus (UserProfile/.botnexus)
```

---

## Schema Version

| Field | JSON Path | Type | Default | Description |
|-------|-----------|------|---------|-------------|
| `version` | `version` | `int` | `1` | Configuration schema version for forward compatibility |

The current supported version is **1**. When the Gateway encounters a `version` higher than the supported version, it emits a trace warning and continues with best-effort compatibility:

> *"version 'N' is newer than supported version '1'. The gateway will continue with best-effort compatibility."*

### Migration Approach

The Gateway uses a single-version model: the current binary supports version `1`. Future breaking changes will increment the version number. The loader validates against the current version — older configs work as-is; newer configs produce a warning but are still loaded.

---

## Root Fields

These fields can be set at the root level or nested under `gateway`. When both exist, the nested `gateway` section takes priority.

| Field | JSON Path | Type | Default | Description |
|-------|-----------|------|---------|-------------|
| `$schema` | `$schema` | `string?` | `null` | JSON Schema reference for editor IntelliSense |
| `version` | `version` | `int` | `1` | Config schema version |
| `gateway` | `gateway` | `object?` | `null` | Nested gateway settings (preferred form) |
| `agents` | `agents` | `object?` | `null` | Agent definitions keyed by agent ID |
| `providers` | `providers` | `object?` | `null` | Provider configurations keyed by provider name |
| `channels` | `channels` | `object?` | `null` | Channel settings keyed by channel name |
| `extensions` | `extensions` | `object?` | `null` | Extension loading settings |
| `apiKey` | `apiKey` | `string?` | `null` | Single API key for Gateway auth (dev/simple mode) |
| `apiKeys` | `apiKeys` | `object?` | `null` | Multi-tenant API keys (root-level legacy form) |
| `listenUrl` | `listenUrl` | `string?` | `null` | Gateway listen URL (root-level legacy form) |
| `defaultAgentId` | `defaultAgentId` | `string?` | `null` | Default agent (root-level legacy form) |
| `agentsDirectory` | `agentsDirectory` | `string?` | `null` | Agents config directory (root-level legacy form) |
| `sessionsDirectory` | `sessionsDirectory` | `string?` | `null` | Sessions storage directory (root-level legacy form) |
| `sessionStore` | `sessionStore` | `object?` | `null` | Session store config (root-level legacy form) |
| `cors` | `cors` | `object?` | `null` | CORS settings (root-level legacy form) |
| `rateLimit` | `rateLimit` | `object?` | `null` | Rate limit settings (root-level legacy form) |
| `logLevel` | `logLevel` | `string?` | `null` | Logging level (root-level legacy form) |

> **Note:** The `gateway` nested form is preferred. Root-level fields (`listenUrl`, `defaultAgentId`, etc.) are the legacy form. When both exist, the nested `gateway.*` value wins. For example, `gateway.listenUrl` takes priority over the root `listenUrl`.

---

## Gateway Settings (`gateway`)

Nested object containing Gateway runtime configuration.

| Field | JSON Path | Type | Default | Validation | Description |
|-------|-----------|------|---------|------------|-------------|
| `listenUrl` | `gateway.listenUrl` | `string?` | `null` | Must be a valid absolute `http://` or `https://` URL | HTTP listen URL |
| `defaultAgentId` | `gateway.defaultAgentId` | `string?` | `null` | — | Default agent when none specified in request |
| `agentsDirectory` | `gateway.agentsDirectory` | `string?` | `null` | Must be a valid path (no invalid path chars) | Path to agents configuration directory |
| `sessionsDirectory` | `gateway.sessionsDirectory` | `string?` | `null` | Must be a valid path (no invalid path chars) | Path to sessions storage directory |
| `sessionStore` | `gateway.sessionStore` | `object?` | `null` | See [Session Store](#session-store-sessionstore) | Session store config |
| `cors` | `gateway.cors` | `object?` | `null` | See [CORS](#cors-cors) | CORS settings |
| `rateLimit` | `gateway.rateLimit` | `object?` | `null` | See [Rate Limiting](#rate-limiting-ratelimit) | Rate limit settings |
| `logLevel` | `gateway.logLevel` | `string?` | `null` | One of: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical` | Logging level |
| `apiKeys` | `gateway.apiKeys` | `object?` | `null` | See [API Keys](#api-keys-apikeys) | Multi-tenant API keys |
| `extensions` | `gateway.extensions` | `object?` | `null` | See [Extensions](#extensions-extensions) | Extension loading settings |

**Example:**

```json
{
  "gateway": {
    "listenUrl": "http://localhost:5005",
    "defaultAgentId": "assistant",
    "logLevel": "Information"
  }
}
```

---

## Agents (`agents`)

Dictionary of agent definitions keyed by agent ID. Each agent connects to a provider and model.

| Field | JSON Path | Type | Default | Validation | Description |
|-------|-----------|------|---------|------------|-------------|
| `provider` | `agents.{id}.provider` | `string` | — | **Required** | Provider name (e.g., `"copilot"`, `"openai"`, `"anthropic"`) |
| `model` | `agents.{id}.model` | `string` | — | **Required** | Model identifier (e.g., `"gpt-4.1"`, `"claude-opus-4.6"`) |
| `systemPromptFile` | `agents.{id}.systemPromptFile` | `string?` | `null` | — | Path to an external system prompt file |
| `isolationStrategy` | `agents.{id}.isolationStrategy` | `string?` | `null` | — | Execution strategy: `"in-process"` |
| `enabled` | `agents.{id}.enabled` | `bool` | `true` | — | Whether the agent is active |

**Validation rules:**
- Agent ID (the dictionary key) must be non-empty.
- `provider` is required.
- `model` is required.

**Example:**

```json
{
  "agents": {
    "assistant": {
      "provider": "copilot",
      "model": "gpt-4.1",
      "isolationStrategy": "in-process",
      "enabled": true
    },
    "researcher": {
      "provider": "copilot",
      "model": "claude-opus-4.6",
      "systemPromptFile": "prompts/researcher.txt",
      "enabled": true
    }
  }
}
```

---

## Providers (`providers`)

Dictionary of provider configurations keyed by provider name. Providers connect agents to upstream LLM APIs.

| Field | JSON Path | Type | Default | Validation | Description |
|-------|-----------|------|---------|------------|-------------|
| `apiKey` | `providers.{name}.apiKey` | `string?` | `null` | At least one of `apiKey` or `baseUrl` required | API key or `"auth:provider-name"` reference |
| `baseUrl` | `providers.{name}.baseUrl` | `string?` | `null` | Must be a valid `http://` or `https://` URL | API base URL |
| `defaultModel` | `providers.{name}.defaultModel` | `string?` | `null` | — | Default model when agent doesn't specify one |

**Validation rules:**
- Provider key must be non-empty.
- At least one of `apiKey` or `baseUrl` must be defined.
- `baseUrl` must be a valid absolute HTTP/HTTPS URL when provided.

### The `"auth:provider-name"` Reference Syntax

The `apiKey` field supports a special reference syntax to delegate authentication to `auth.json`:

```json
{
  "providers": {
    "copilot": {
      "apiKey": "auth:copilot",
      "baseUrl": "https://api.githubcopilot.com"
    }
  }
}
```

When `apiKey` starts with `"auth:"`, the Gateway:
1. Strips the `"auth:"` prefix to get the provider name (`"copilot"`).
2. Looks up that provider in `~/.botnexus/auth.json`.
3. Returns the `access` token from the auth entry.
4. Automatically refreshes OAuth tokens when expired.

This avoids storing raw API keys in `config.json`.

**Example values:**

| `apiKey` Value | Resolution |
|----------------|------------|
| `"auth:copilot"` | Looks up `copilot` in auth.json |
| `"auth:github-copilot"` | Looks up `github-copilot` in auth.json |
| `"sk-abc123..."` | Uses the literal value as the API key |

**Example:**

```json
{
  "providers": {
    "copilot": {
      "apiKey": "auth:copilot",
      "baseUrl": "https://api.githubcopilot.com",
      "defaultModel": "gpt-4.1"
    },
    "openai": {
      "apiKey": "sk-...",
      "baseUrl": "https://api.openai.com/v1",
      "defaultModel": "gpt-4o"
    }
  }
}
```

---

## Channels (`channels`)

Dictionary of channel configurations keyed by channel name.

| Field | JSON Path | Type | Default | Validation | Description |
|-------|-----------|------|---------|------------|-------------|
| `type` | `channels.{name}.type` | `string` | — | **Required** | Channel type (e.g., `"websocket"`, `"slack"`, `"telegram"`) |
| `enabled` | `channels.{name}.enabled` | `bool` | `true` | — | Whether the channel is active |
| `settings` | `channels.{name}.settings` | `object?` | `null` | — | Adapter-specific key-value settings |

**Validation rules:**
- Channel key must be non-empty.
- `type` is required.

**Example:**

```json
{
  "channels": {
    "web": {
      "type": "websocket",
      "enabled": true
    },
    "telegram": {
      "type": "telegram",
      "enabled": true,
      "settings": {
        "botToken": "123456:ABC-DEF..."
      }
    }
  }
}
```

---

## Extensions (`extensions`)

Configuration for dynamic extension discovery and loading.

| Field | JSON Path | Type | Default | Validation | Description |
|-------|-----------|------|---------|------------|-------------|
| `path` | `extensions.path` | `string?` | `null` | — | Root directory containing extension folders with `botnexus-extension.json` manifests |
| `enabled` | `extensions.enabled` | `bool` | `true` | — | Enables or disables dynamic extension loading |

Can be set at the root level or nested under `gateway.extensions`. The nested form takes priority.

**Example:**

```json
{
  "extensions": {
    "path": "~/.botnexus/extensions",
    "enabled": true
  }
}
```

---

## API Keys (`apiKeys`)

Multi-tenant API keys for Gateway endpoint protection. When at least one key is configured, the `GatewayAuthMiddleware` enforces authentication on all endpoints except `/health`, `/webui`, and `/swagger`.

Can be set at the root level or nested under `gateway.apiKeys`. The nested form takes priority.

### ApiKeyConfig Fields

| Field | JSON Path | Type | Default | Validation | Description |
|-------|-----------|------|---------|------------|-------------|
| `apiKey` | `apiKeys.{id}.apiKey` | `string` | — | **Required** | The secret key value |
| `tenantId` | `apiKeys.{id}.tenantId` | `string` | — | **Required** | Tenant identifier for multi-tenant isolation |
| `callerId` | `apiKeys.{id}.callerId` | `string?` | `null` | — | Caller identifier used in audit logs |
| `displayName` | `apiKeys.{id}.displayName` | `string?` | `null` | — | Human-readable label |
| `allowedAgents` | `apiKeys.{id}.allowedAgents` | `string[]?` | `null` | — | Agent IDs this key can access (empty/null = all) |
| `permissions` | `apiKeys.{id}.permissions` | `string[]` | — | **Required**, at least one entry | Granted scopes |
| `isAdmin` | `apiKeys.{id}.isAdmin` | `bool` | `false` | — | Full unrestricted access |

**Validation rules:**
- Key ID (dictionary key) must be non-empty.
- `apiKey` is required.
- `tenantId` is required.
- `permissions` must contain at least one entry.

**Known permissions:** `chat:send`, `sessions:read`

**Example:**

```json
{
  "gateway": {
    "apiKeys": {
      "dev-key": {
        "apiKey": "sk-my-dev-key",
        "tenantId": "development",
        "callerId": "developer",
        "displayName": "Development Key",
        "allowedAgents": [],
        "permissions": ["chat:send", "sessions:read"],
        "isAdmin": true
      },
      "limited-key": {
        "apiKey": "sk-limited-key",
        "tenantId": "external",
        "callerId": "partner-app",
        "displayName": "Partner API Key",
        "allowedAgents": ["assistant"],
        "permissions": ["chat:send"],
        "isAdmin": false
      }
    }
  }
}
```

---

## Session Store (`sessionStore`)

Configures how the Gateway persists conversation sessions.

| Field | JSON Path | Type | Default | Validation | Description |
|-------|-----------|------|---------|------------|-------------|
| `type` | `sessionStore.type` | `string?` | `null` | `"InMemory"` or `"File"` | Session storage implementation |
| `filePath` | `sessionStore.filePath` | `string?` | `null` | **Required** when `type` is `"File"`; must be a valid path | Directory for file-based session storage |

Can be set at the root level or nested under `gateway.sessionStore`. The nested form takes priority.

**Example (in-memory):**

```json
{
  "gateway": {
    "sessionStore": {
      "type": "InMemory"
    }
  }
}
```

**Example (file-based):**

```json
{
  "gateway": {
    "sessionStore": {
      "type": "File",
      "filePath": "~/.botnexus/sessions"
    }
  }
}
```

---

## CORS (`cors`)

Cross-Origin Resource Sharing settings for browser-based clients.

| Field | JSON Path | Type | Default | Validation | Description |
|-------|-----------|------|---------|------------|-------------|
| `allowedOrigins` | `cors.allowedOrigins` | `string[]?` | `null` | Each entry must be a valid `http://` or `https://` absolute URL | Explicit origins allowed to access the Gateway |

Can be set at the root level or nested under `gateway.cors`. The nested form takes priority.

**Example:**

```json
{
  "gateway": {
    "cors": {
      "allowedOrigins": [
        "http://localhost:3000",
        "https://my-app.example.com"
      ]
    }
  }
}
```

---

## Rate Limiting (`rateLimit`)

Per-client HTTP request rate limiting.

| Field | JSON Path | Type | Default | Validation | Description |
|-------|-----------|------|---------|------------|-------------|
| `requestsPerMinute` | `rateLimit.requestsPerMinute` | `int` | `60` | — | Maximum requests per client per window |
| `windowSeconds` | `rateLimit.windowSeconds` | `int` | `60` | — | Rate limit window duration (seconds) |

Can be set at the root level or nested under `gateway.rateLimit`. The nested form takes priority.

> **Note:** This controls HTTP REST endpoint rate limiting. WebSocket connection throttling is configured separately via `GatewayWebSocketOptions` (see [WebSocket Protocol Spec](websocket-protocol.md#rate-limiting-and-backpressure)).

**Example:**

```json
{
  "gateway": {
    "rateLimit": {
      "requestsPerMinute": 120,
      "windowSeconds": 60
    }
  }
}
```

---

## Auth File (`auth.json`)

The auth file stores provider authentication credentials separately from the main config. It is located at `~/.botnexus/auth.json`.

### Format

The file is a JSON dictionary keyed by provider name. Each entry contains authentication credentials:

```json
{
  "copilot": {
    "type": "oauth",
    "access": "ghu_xxxxxxxxxxxx",
    "refresh": "ghr_xxxxxxxxxxxx",
    "expires": 1234567890000,
    "endpoint": "https://api.githubcopilot.com"
  }
}
```

### Auth Entry Fields

| Field | Type | Description |
|-------|------|-------------|
| `type` | `string` | Auth type. Currently only `"oauth"` is supported. |
| `access` | `string` | Access token (bearer token for API calls) |
| `refresh` | `string` | Refresh token for token renewal |
| `expires` | `long` | Token expiry timestamp in **milliseconds** since Unix epoch |
| `endpoint` | `string?` | API endpoint override (e.g., enterprise Copilot endpoint) |

### Auth Resolution Order

When the Gateway resolves an API key for a provider, it checks these sources in order:

| Priority | Source | Description |
|----------|--------|-------------|
| 1 | `~/.botnexus/auth.json` | OAuth tokens and enterprise endpoints |
| 2 | Environment variables | `BOTNEXUS_COPILOT_APIKEY`, `BOTNEXUS_OPENAI_APIKEY`, etc. |
| 3 | `config.json` providers section | `providers.{name}.apiKey` field (literal or `"auth:"` reference) |

### Referencing auth.json from config.json

Use the `"auth:provider-name"` syntax in a provider's `apiKey` field:

```json
{
  "providers": {
    "copilot": {
      "apiKey": "auth:copilot"
    }
  }
}
```

This tells the Gateway to look up the `"copilot"` entry in `auth.json` and use its `access` token. The `"copilot"` and `"github-copilot"` keys are treated as aliases.

### OAuth Token Refresh

For `"type": "oauth"` entries, the Gateway automatically refreshes tokens when:
- The current time is within 60 seconds of the `expires` timestamp.
- The `endpoint` field is empty (triggers a refresh to discover the endpoint).

Refreshed tokens are written back to `auth.json` automatically.

### Legacy Auth Path

The Gateway also checks a legacy auth path at `./.botnexus-agent/auth.json` (relative to the working directory). Entries from both paths are merged, with the primary `~/.botnexus/auth.json` taking priority for duplicate keys.

---

## Full Annotated Example

```json
{
  "$schema": "docs/botnexus-config.schema.json",
  "version": 1,

  "gateway": {
    "listenUrl": "http://localhost:5005",
    "defaultAgentId": "assistant",
    "agentsDirectory": null,
    "sessionsDirectory": null,
    "logLevel": "Information",

    "sessionStore": {
      "type": "File",
      "filePath": "~/.botnexus/sessions"
    },

    "cors": {
      "allowedOrigins": [
        "http://localhost:3000"
      ]
    },

    "rateLimit": {
      "requestsPerMinute": 60,
      "windowSeconds": 60
    },

    "extensions": {
      "path": "~/.botnexus/extensions",
      "enabled": true
    },

    "apiKeys": {
      "dev": {
        "apiKey": "sk-dev-key-123",
        "tenantId": "development",
        "callerId": "developer",
        "displayName": "Dev Key",
        "allowedAgents": [],
        "permissions": ["chat:send", "sessions:read"],
        "isAdmin": true
      }
    }
  },

  "agents": {
    "assistant": {
      "provider": "copilot",
      "model": "gpt-4.1",
      "isolationStrategy": "in-process",
      "enabled": true
    },
    "researcher": {
      "provider": "copilot",
      "model": "claude-opus-4.6",
      "systemPromptFile": "prompts/researcher.txt",
      "enabled": true
    }
  },

  "providers": {
    "copilot": {
      "apiKey": "auth:copilot",
      "baseUrl": "https://api.githubcopilot.com",
      "defaultModel": "gpt-4.1"
    },
    "openai": {
      "apiKey": "sk-...",
      "baseUrl": "https://api.openai.com/v1",
      "defaultModel": "gpt-4o"
    }
  },

  "channels": {
    "web": {
      "type": "websocket",
      "enabled": true
    }
  }
}
```

---

## JSON Schema Validation

BotNexus provides a generated JSON Schema for config validation and editor IntelliSense.

### Using the Schema

Reference the schema in your `config.json`:

```json
{
  "$schema": "docs/botnexus-config.schema.json",
  "version": 1
}
```

### Programmatic Validation

The Gateway validates config files on load using both JSON Schema validation and semantic validation:

```
PlatformConfigLoader.Load(configPath)
  1. Deserialize JSON → PlatformConfig
  2. PlatformConfigSchema.ValidateJson(rawJson) → schema errors
  3. PlatformConfigLoader.Validate(config) → semantic errors
  4. Throw if any errors
```

### Runtime Validation Endpoint

The running Gateway exposes a validation endpoint:

```
GET /api/config/validate
```

### Hot Reload

The Gateway watches `config.json` with a `FileSystemWatcher`. Changes are debounced (500ms) and applied automatically — no restart needed for most settings.

**Hot-reloadable:** Agent definitions, provider settings, channel settings, API key configuration.

**Requires restart:** `listenUrl` changes (port binding), new isolation strategy registrations, extension DLL additions.

---

## See Also

- [Developer Guide](dev-guide.md) — Local development setup and configuration walkthrough
- [WebSocket Protocol](websocket-protocol.md) — WebSocket connection limits and backpressure configuration
- [API Reference](api-reference.md) — REST and WebSocket endpoint documentation
- [Architecture Overview](architecture.md) — System design and component responsibilities
