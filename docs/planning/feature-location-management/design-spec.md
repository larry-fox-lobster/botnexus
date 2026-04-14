---
id: feature-location-management
title: "Location Management"
type: feature
priority: high
status: draft
created: 2026-07-14
author: copilot
tags: [locations, configuration, file-access, path-validation, cli, webui, architecture]
ddd_types: [Location, LocationType, WorldDescriptor, FileAccessPolicy, DefaultPathValidator, PlatformConfig]
---

# Design Spec: Location Management

**Type**: Feature
**Priority**: High
**Status**: Draft
**Author**: Copilot

## Problem

BotNexus has no centralized way to declare, manage, and reference the resources a gateway knows about. Locations exist today as a domain concept (`Location`, `LocationType`, `WorldDescriptor`) but are **derived programmatically** inside `WorldDescriptorBuilder` from scattered config properties. They cannot be explicitly declared, browsed, validated, or referenced by other features.

This causes three concrete problems:

1. **File access policies use hardcoded paths.** When an agent needs read access to `Q:\repos\botnexus`, the operator writes the raw path in `fileAccess.allowedReadPaths`. If the repo moves, every policy referencing that path must be updated manually. There is no indirection.

2. **No resource inventory.** Operators have no single view of what filesystem paths, APIs, MCP servers, and databases the gateway uses. The only way to discover locations is to read `WorldDescriptor` at runtime via the API — there is no CLI or UI surface.

3. **No health validation.** There is no way to verify that configured paths exist, APIs respond, or databases connect. The `validate` command checks config syntax but not resource accessibility.

### Why Locations Matter

Locations are the platform's resource registry. Any capability that needs path/endpoint access should reference a location rather than hardcode connection details. This enables:

- **Validation via `botnexus doctor`** — check that all resources are accessible
- **Consistent UI management** — browse and edit locations in the WebUI
- **Cross-agent resource sharing** — agents reference well-known location names
- **Environment portability** — change a location path once, all references update

## Requirements

### Must Have

1. **Location config section** — `gateway.locations` in `config.json` as a dictionary of named location definitions, each with `type`, connection detail (`path`/`endpoint`/`connectionString`), optional `description`, and optional `properties`.
2. **Location reference syntax** — `@location-name` prefix in `fileAccess.allowedReadPaths`, `allowedWritePaths`, and `deniedPaths` that resolves to the location's path. Sub-paths supported: `@repo/docs/planning`.
3. **Backward compatibility** — Raw paths (without `@`) continue to work unchanged.
4. **Merged location registry** — `WorldDescriptorBuilder` merges user-declared locations with auto-derived locations (agent workspaces, providers, MCP servers). User-declared locations take precedence on name collision.
5. **Domain model update** — Add `Description` property to `Location` record.
6. **CLI `locations list`** — Display all registered locations with name, type, path, description, and status.

### Should Have

7. **CLI `locations add/update/delete`** — CRUD operations that modify `config.json` directly.
8. **CLI `doctor locations`** — Validate all locations are accessible (paths exist, APIs respond, DBs connect).
9. **Config validation** — `PlatformConfigLoader.Validate()` checks that `@location-ref` values in file access policies resolve to defined locations.
10. **Per-agent location access** — Agents inherit world-level locations but can have additional agent-scoped locations.

### Nice to Have

11. **WebUI locations view** — Sidebar section showing all locations with CRUD and health check UI.
12. **Location health events** — SignalR real-time status updates for location health changes.
13. **Location dependency tracking** — Warn when deleting a location referenced by policies.
14. **Environment variable expansion** — Support `${ENV_VAR}` in location paths for portability.

## Architecture

### Config Schema

Add a `Locations` dictionary to `GatewaySettingsConfig`:

```json
{
  "gateway": {
    "locations": {
      "repo-botnexus": {
        "type": "filesystem",
        "path": "Q:/repos/botnexus",
        "description": "BotNexus source repository"
      },
      "copilot-api": {
        "type": "api",
        "endpoint": "https://api.enterprise.githubcopilot.com",
        "description": "GitHub Copilot API"
      },
      "memory-db": {
        "type": "database",
        "connectionString": "Data Source=~/.botnexus/agents/{agentId}/data/memory.sqlite",
        "description": "Agent memory store"
      }
    },
    "fileAccess": {
      "allowedReadPaths": ["@repo-botnexus", "@docs"],
      "deniedPaths": ["@repo-botnexus/.env"]
    }
  },
  "agents": {
    "nova": {
      "fileAccess": {
        "allowedWritePaths": ["@docs/planning"]
      }
    }
  }
}
```

#### Config Model

```csharp
// In PlatformConfig.cs

public sealed class GatewaySettingsConfig
{
    // ... existing properties ...

    /// <summary>Named location definitions for the gateway's resource registry.</summary>
    public Dictionary<string, LocationConfig>? Locations { get; set; }
}

/// <summary>A named location in the gateway's resource registry.</summary>
public sealed class LocationConfig
{
    /// <summary>Location type (filesystem, api, mcp-server, remote-node, database).</summary>
    public string? Type { get; set; }

    /// <summary>Filesystem path (for filesystem type).</summary>
    public string? Path { get; set; }

    /// <summary>API endpoint URL (for api type).</summary>
    public string? Endpoint { get; set; }

    /// <summary>Database connection string (for database type).</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Human-readable description.</summary>
    public string? Description { get; set; }

    /// <summary>Extensible key-value properties.</summary>
    public Dictionary<string, string>? Properties { get; set; }
}
```

#### Connection Detail Resolution

Each `LocationConfig` carries type-specific connection details. The effective connection detail is resolved by type:

| LocationType | Primary Field | Fallback |
|-------------|---------------|----------|
| `filesystem` | `Path` | — |
| `api` | `Endpoint` | `Path` |
| `mcp-server` | `Endpoint` | `Path` |
| `remote-node` | `Endpoint` | `Path` |
| `database` | `ConnectionString` | `Path` |

If the type-specific field is null, fall back to `Path` for backward compatibility.

### Location Resolution Service

A new `ILocationResolver` interface resolves `@location-ref` strings to absolute paths:

```csharp
// In BotNexus.Gateway.Contracts

/// <summary>
/// Resolves location references (@name or @name/sub/path) to absolute paths.
/// </summary>
public interface ILocationResolver
{
    /// <summary>
    /// Resolves a path that may contain a @location-ref prefix.
    /// Returns the resolved absolute path, or the original path if no @ prefix.
    /// </summary>
    string? Resolve(string path);

    /// <summary>
    /// Returns the Location for the given name, or null if not found.
    /// </summary>
    Location? GetLocation(string name);

    /// <summary>
    /// Returns all registered locations.
    /// </summary>
    IReadOnlyList<Location> GetAll();
}
```

#### Resolution Algorithm

```
Input: "@repo-botnexus/docs/planning"

1. Detect @ prefix → strip @
2. Split on first "/" → name = "repo-botnexus", subPath = "docs/planning"
3. Look up name in location registry → Location { Path = "Q:/repos/botnexus" }
4. Combine: Path.Combine("Q:/repos/botnexus", "docs/planning")
5. Normalize: Path.GetFullPath(...)
6. Return: "Q:\repos\botnexus\docs\planning"

Input: "Q:/repos/botnexus" (no @ prefix)
→ Return unchanged (backward compat)

Input: "@nonexistent"
→ Log warning, return null (policy validation catches this)
```

### Policy Integration

File access policy paths flow through this pipeline:

```
config.json (raw strings, may contain @refs)
    ↓
PlatformConfigAgentSource.MapFileAccessPolicy()
    ↓
FileAccessPolicy (domain record, still raw strings)
    ↓
DefaultPathValidator constructor → ResolvePolicyPaths()
    ↓
Resolved absolute paths (@ refs expanded)
```

**Integration point:** Modify `DefaultPathValidator.ResolvePolicyPaths()` to call `ILocationResolver.Resolve()` for any path starting with `@`. This keeps the change surgical — `DefaultPathValidator` already does path resolution, it just gains a new resolution strategy for `@`-prefixed paths.

```csharp
// In DefaultPathValidator

private readonly ILocationResolver? _locationResolver;

public DefaultPathValidator(
    FileAccessPolicy? policy,
    string workspacePath,
    ILocationResolver? locationResolver = null)
{
    // ... existing init ...
    _locationResolver = locationResolver;
    _allowedReadPaths = ResolvePolicyPaths(policy?.AllowedReadPaths);
    // ...
}

private IReadOnlyList<string> ResolvePolicyPaths(IReadOnlyList<string>? paths)
{
    if (paths is null || paths.Count == 0)
        return [];

    var resolved = new List<string>(paths.Count);
    foreach (var path in paths)
    {
        if (string.IsNullOrWhiteSpace(path))
            continue;

        // Location reference resolution
        if (path.StartsWith('@') && _locationResolver is not null)
        {
            var resolvedPath = _locationResolver.Resolve(path[1..]);
            if (resolvedPath is not null)
                resolved.Add(IsGlobPattern(resolvedPath)
                    ? ResolveGlobPath(resolvedPath)
                    : NormalizePath(resolvedPath));
            continue;
        }

        resolved.Add(IsGlobPattern(path) ? ResolveGlobPath(path) : ResolvePath(path));
    }

    return resolved;
}
```

### WorldDescriptorBuilder Merge

`WorldDescriptorBuilder.ResolveLocations()` is updated to merge sources:

```
1. Load user-declared locations from gateway.locations config
2. Build auto-derived locations (agent workspaces, providers, MCP servers) — existing logic
3. Merge: user-declared locations win on name collision
4. Sort alphabetically by name
```

The `Location` domain record gains a `Description` property:

```csharp
public sealed record Location
{
    public required string Name { get; init; }
    public required LocationType Type { get; init; }
    public string? Path { get; init; }
    public string? Description { get; init; }  // NEW
    public IReadOnlyDictionary<string, string> Properties { get; init; }
        = new Dictionary<string, string>();
}
```

### CLI Commands

#### `botnexus locations list`

```
$ botnexus locations list

Name                    Type         Path/Endpoint                              Description
────                    ────         ─────────────                              ───────────
agents-directory        filesystem   C:\Users\jon\.botnexus\agents              (auto-derived)
agent:nova:workspace    filesystem   C:\Users\jon\.botnexus\agents\nova\ws      (auto-derived)
copilot-api             api          https://api.enterprise.githubcopilot.com   GitHub Copilot API
docs                    filesystem   Q:\repos\botnexus\docs                     Documentation
memory-db               database     Data Source=~/.botnexus/.../memory.sqlite  Agent memory store
repo-botnexus           filesystem   Q:\repos\botnexus                          BotNexus source repository

6 locations (3 declared, 3 auto-derived)
```

#### `botnexus locations add`

```
$ botnexus locations add my-repo --type filesystem --path "Q:/repos/myrepo" --description "My repository"
Added location 'my-repo'.
```

Modifies `config.json` directly. Validates:
- Name is unique (not colliding with declared or auto-derived)
- Type is a valid `LocationType` value
- Path/endpoint is non-empty
- For filesystem type: warns if path does not exist

#### `botnexus locations update`

```
$ botnexus locations update my-repo --path "Q:/repos/myrepo-v2"
Updated location 'my-repo'.
```

Only updates user-declared locations. Auto-derived locations cannot be updated (they change via their source config properties).

#### `botnexus locations delete`

```
$ botnexus locations delete my-repo
Warning: Location 'my-repo' is referenced by fileAccess policies in agents: nova
Delete anyway? This will leave dangling @my-repo references. [y/N]: y
Deleted location 'my-repo'.
```

#### `botnexus doctor locations`

```
$ botnexus doctor locations

Checking 6 locations...

✅ agents-directory        Q:\...\.botnexus\agents              exists, writable
✅ repo-botnexus           Q:\repos\botnexus                    exists, readable
✅ docs                    Q:\repos\botnexus\docs               exists, readable
⚠️  copilot-api            https://api.enterprise.github...     HTTP 401 (auth required)
❌ memory-db               Data Source=...memory.sqlite          file not found
✅ agent:nova:workspace    Q:\...\.botnexus\agents\nova\ws      exists, writable

Results: 4 healthy, 1 warning, 1 error
```

Health checks by type:

| Type | Check |
|------|-------|
| `filesystem` | `Directory.Exists()` or `File.Exists()`, test read/write permission |
| `api` | HTTP HEAD request, check for 2xx/401 (auth-required is a warning, not error) |
| `database` | Attempt connection open with short timeout |
| `mcp-server` | Check if command exists on PATH |
| `remote-node` | HTTP connectivity check |

### WebUI Locations View

#### Layout

```
┌─────────────────────────────────────────────────────┐
│ 📍 Locations                           [+ Add] [🔍] │
├─────────────────────────────────────────────────────┤
│ ✅ repo-botnexus     📁 filesystem                  │
│    Q:\repos\botnexus                                │
│    BotNexus source repository          [✓] [✏️] [🗑] │
├─────────────────────────────────────────────────────┤
│ ✅ docs              📁 filesystem                  │
│    Q:\repos\botnexus\docs                           │
│    Documentation                       [✓] [✏️] [🗑] │
├─────────────────────────────────────────────────────┤
│ ⚠️ copilot-api       🌐 api                         │
│    https://api.enterprise.githubcopilot.com         │
│    GitHub Copilot API                  [✓] [✏️] [🗑] │
├─────────────────────────────────────────────────────┤
│ ✅ agents-directory   📁 filesystem    (auto)        │
│    C:\Users\jon\.botnexus\agents                    │
│    (auto-derived from gateway config)  [✓]          │
└─────────────────────────────────────────────────────┘
```

- `[✓]` — Run health check for this location
- `[✏️]` — Edit (only for user-declared locations)
- `[🗑]` — Delete (only for user-declared, with reference check)
- Auto-derived locations are read-only with `(auto)` badge

#### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/locations` | List all locations with status |
| `POST` | `/api/locations` | Add a new location |
| `PUT` | `/api/locations/{name}` | Update a location |
| `DELETE` | `/api/locations/{name}` | Delete a location |
| `POST` | `/api/locations/{name}/check` | Run health check |
| `POST` | `/api/locations/check` | Run health check on all locations |

## Implementation Phases

### Phase 1: Config and Domain (Foundation)

**Changes:**
1. Add `Description` property to `Location` domain record
2. Add `LocationConfig` class and `Locations` dictionary to `GatewaySettingsConfig`
3. Update `WorldDescriptorBuilder.ResolveLocations()` to merge user-declared locations
4. Add validation rules to `PlatformConfigLoader.Validate()` for location definitions
5. Regenerate JSON schema via `botnexus config schema`

**Files modified:**
- `src/domain/BotNexus.Domain/World/Location.cs`
- `src/gateway/BotNexus.Gateway/Configuration/PlatformConfig.cs`
- `src/gateway/BotNexus.Gateway/Configuration/WorldDescriptorBuilder.cs`
- `src/gateway/BotNexus.Gateway/Configuration/PlatformConfigLoader.cs`
- `docs/botnexus-config.schema.json`

**Tests:**
- `WorldDescriptorBuilder` merges declared + derived locations
- Declared locations take precedence on name collision
- `Location.Description` round-trips through serialization

### Phase 2: Location References in Policies

**Changes:**
1. Create `ILocationResolver` interface in Gateway contracts
2. Create `DefaultLocationResolver` implementation
3. Inject `ILocationResolver` into `DefaultPathValidator`
4. Update `ResolvePolicyPaths()` to resolve `@` prefixes
5. Update `DefaultAgentToolFactory` to pass resolver when constructing path validators
6. Add config validation: warn on unresolvable `@` references in policies

**Files modified:**
- `src/gateway/BotNexus.Gateway.Contracts/` — new `ILocationResolver.cs`
- `src/gateway/BotNexus.Gateway/Configuration/` — new `DefaultLocationResolver.cs`
- `src/gateway/BotNexus.Gateway/Security/DefaultPathValidator.cs`
- `src/gateway/BotNexus.Gateway/Agents/DefaultAgentToolFactory.cs`

**Tests:**
- `@repo-botnexus` resolves to location path
- `@repo-botnexus/docs/planning` resolves to sub-path
- `@nonexistent` returns null and logs warning
- Raw paths (no `@`) unchanged (backward compat)
- Deny paths with `@` references work correctly
- Glob patterns combined with `@` references

### Phase 3: CLI Commands

**Changes:**
1. Create `LocationsCommand` class with `list`, `add`, `update`, `delete` sub-commands
2. Create `DoctorCommand` class with `locations` sub-command
3. Register in `Program.cs` DI container and root command
4. Add location health check logic (filesystem existence, HTTP probes, DB connection tests)

**Files modified:**
- `src/gateway/BotNexus.Cli/Commands/` — new `LocationsCommand.cs`, new `DoctorCommand.cs`
- `src/gateway/BotNexus.Cli/Program.cs`

**Tests:**
- CLI integration tests for list, add, update, delete
- Doctor location checks for each location type

### Phase 4: WebUI (Nice to Have)

**Changes:**
1. Add location API endpoints to gateway
2. Create `locations.js` module in WebUI
3. Add sidebar section and canvas view
4. Wire up CRUD operations and health checks
5. Add SignalR events for location status changes

**Files modified:**
- `src/gateway/BotNexus.Gateway.Api/` — new location endpoints
- `src/BotNexus.WebUI/wwwroot/js/` — new `locations.js`
- `src/BotNexus.WebUI/wwwroot/index.html` — sidebar section
- `src/BotNexus.WebUI/wwwroot/styles.css` — location styles

## Edge Cases

### Resolution Conflicts

**Name collision between declared and auto-derived locations:**
User-declared locations always win. Auto-derived locations with the same name are silently overridden. A config validation warning is emitted so operators know about the collision.

**Circular references:**
Location paths cannot reference other locations (`@loc-a` pointing to `@loc-b`). The resolver operates in a single pass — no recursion. If a location's path starts with `@`, it is treated as a literal path.

### Path Normalization

**Windows vs. Unix paths:**
`DefaultPathValidator` already handles platform-specific path comparison (`StringComparison.OrdinalIgnoreCase` on Windows). Location paths are normalized through the same pipeline.

**Tilde expansion in location paths:**
Location paths support `~` expansion via `DefaultPathValidator.ExpandUserHome()`. Example: `"path": "~/repos/botnexus"`.

**Template variables:**
The `{agentId}` placeholder in connection strings (e.g., `memory-db`) is resolved at agent context creation time, not at location registration time. The location stores the template; the consumer resolves it.

### Deletion Safety

**Deleting a referenced location:**
The CLI `delete` command scans all `fileAccess` policies (world-level and per-agent) for `@name` references. If found, it warns the operator and requires confirmation. The WebUI shows a dialog with the list of referencing policies.

**Deleting auto-derived locations:**
Not possible. Auto-derived locations are rebuilt on every config reload. To remove them, remove the source config (e.g., disable an agent to remove its workspace location).

### Migration

**No migration needed.** The `locations` section is additive and optional. Existing configs without it continue to work — `WorldDescriptorBuilder` still auto-derives locations as before. The `@` reference syntax is opt-in.

### Invalid Location References

**`@nonexistent` in policy paths:**
- At config load time: `PlatformConfigLoader.ValidateWarnings()` emits a warning
- At policy resolution time: `DefaultPathValidator.ResolvePolicyPaths()` skips the entry and logs a warning
- Net effect: the path is silently dropped from the effective policy (conservative — doesn't grant access to anything)

### Type Mismatch

**`@api-location` used in `allowedReadPaths`:**
Only `filesystem` type locations are meaningful for file access policies. Using a non-filesystem location in a file policy path should emit a config validation warning. At runtime, the resolver returns the location's `Path` value regardless of type — if it happens to be a valid filesystem path, it works; otherwise, `DefaultPathValidator` normalizes it and it won't match any real files.

## Testing Plan

### Unit Tests

| Test | Scope | Description |
|------|-------|-------------|
| `LocationConfig_RoundTrip` | Config | Serialize/deserialize `LocationConfig` through JSON |
| `Location_Description_Property` | Domain | Verify `Description` on `Location` record |
| `WorldDescriptorBuilder_MergesUserLocations` | Builder | User-declared + auto-derived locations merge correctly |
| `WorldDescriptorBuilder_UserLocationsPrecedence` | Builder | User location wins on name collision with auto-derived |
| `LocationResolver_ResolvesSimpleRef` | Resolver | `@name` → location path |
| `LocationResolver_ResolvesSubPath` | Resolver | `@name/sub/path` → combined path |
| `LocationResolver_UnknownRef_ReturnsNull` | Resolver | `@nonexistent` → null |
| `LocationResolver_RawPath_PassThrough` | Resolver | Path without `@` returned unchanged |
| `PathValidator_WithLocationRefs` | Security | `@` refs in AllowedReadPaths resolve and enforce correctly |
| `PathValidator_LocationRef_DenyList` | Security | `@` refs in DeniedPaths work |
| `PathValidator_BackwardCompat` | Security | Raw paths still work when resolver is present |
| `PathValidator_NullResolver_SkipsRefs` | Security | `@` refs ignored when no resolver (backward compat) |
| `ConfigValidation_UnresolvableRef_Warning` | Validation | Config with `@nonexistent` produces validation warning |
| `ConfigValidation_NonFilesystemRef_Warning` | Validation | `@api-location` in file policy produces warning |

### Integration Tests

| Test | Scope | Description |
|------|-------|-------------|
| `LocationsCli_List` | CLI | `locations list` outputs all locations |
| `LocationsCli_AddAndList` | CLI | `locations add` + `locations list` shows new location |
| `LocationsCli_DeleteWithWarning` | CLI | `locations delete` warns about referenced location |
| `DoctorLocations_FileSystem` | CLI | Doctor checks filesystem location existence |
| `AgentFileAccess_WithLocationRef` | E2E | Agent with `@repo` in policy can read repo files |
| `AgentFileAccess_MixedRefs` | E2E | Mix of `@refs` and raw paths in same policy |

### Manual Testing Checklist

- [ ] Add locations to config.json, verify `botnexus locations list` shows them
- [ ] Use `@location-name` in `fileAccess.allowedReadPaths`, verify agent can read files
- [ ] Use `@location-name/sub/path` in `allowedWritePaths`, verify agent can write
- [ ] Run `botnexus doctor locations`, verify health checks for each type
- [ ] Delete a location, verify warning about policy references
- [ ] Verify existing configs without `locations` section still work unchanged
- [ ] Verify `botnexus config schema` regenerates schema with `locations` section
