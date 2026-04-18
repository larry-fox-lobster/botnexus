# Extension Development Rules

## Manifest (`botnexus-extension.json`)

Every extension MUST have a `botnexus-extension.json` in its project root with `CopyToOutputDirectory=PreserveNewest` in the csproj.

### Required fields

| Field | Type | Rule |
|-------|------|------|
| `id` | string | `botnexus-{name}` prefix. Lowercase, kebab-case. Must be unique across all extensions. |
| `name` | string | Human-readable display name. Concise, no "Extension" suffix. |
| `description` | string | One-line description of what the extension does. |
| `version` | string | SemVer. Use `1.0.0` for production extensions. |
| `entryAssembly` | string | DLL filename. Must match the project's `<AssemblyName>` + `.dll`. |
| `extensionTypes` | string[] | One or more of the allowed types (see below). |

### Allowed extension types

| Type | Contract | Use when |
|------|----------|----------|
| `tool` | `IAgentTool` | Extension provides tools agents can call |
| `command` | `ICommandContributor` | Extension adds slash commands |
| `channel` | `IChannelAdapter` | Extension provides a communication channel |
| `media-handler` | `IMediaHandler` | Extension processes media content |
| `endpoint-contributor` | `IEndpointContributor` | Extension registers web endpoints, static files, or middleware |
| `api-contributor` | `IApiContributor` | Extension adds scoped REST API routes |

### Example

```json
{
  "id": "botnexus-example",
  "name": "Example Tool",
  "description": "Short description of what this extension does",
  "version": "1.0.0",
  "entryAssembly": "BotNexus.Extensions.Example.dll",
  "extensionTypes": ["tool"]
}
```

## Naming conventions

- **Project folder**: `BotNexus.Extensions.{Name}/` directly under `src/extensions/` — no extra nesting.
- **Namespace**: `BotNexus.Extensions.{Name}` (channels use `BotNexus.Extensions.Channels.{Name}`).
- **Assembly**: Same as namespace.
- **Manifest ID**: `botnexus-{kebab-name}` (e.g., `botnexus-mcp`, `botnexus-signalr`).

## Formatting

- 2-space JSON indentation.
- No trailing commas.
- No comments in JSON.

## Endpoint extensions

Extensions declaring `endpoint-contributor` or `api-contributor`:
- Are loaded as **non-collectible** assemblies (ASP.NET uses Reflection.Emit for typed hub proxies).
- Must register middleware/endpoints in `MapEndpoints(WebApplication app)`.
- Static files should be co-located with the extension assembly, not in the gateway's wwwroot.

## Enums in SignalR payloads

Any enum type sent through SignalR **must** have `[JsonConverter(typeof(JsonStringEnumConverter))]`. SignalR uses default System.Text.Json which serializes enums as numbers. Clients expect strings.
