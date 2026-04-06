# Provider/Model Configuration — 3-Layer Filtering Pipeline

**Date:** 2026-04-08  
**Author:** Leela (Lead/Architect)  
**Requested by:** Jon Bullen  
**Status:** Proposal — awaiting team decision  
**Scope:** Platform config schema, provider activation, per-provider model allowlists, per-agent model restrictions

---

## 1. Problem Statement

The platform currently registers **all** built-in models from all three providers (github-copilot, anthropic, openai) unconditionally in `BuiltInModels.RegisterAll()`. There is no concept of:

- **Active vs inactive providers** — All providers appear in `GET /api/providers` regardless of authentication status or user preference.
- **Model allowlists** — All 30+ models are returned by `GET /api/models` regardless of whether the user has access.
- **Per-agent model restrictions** — `AgentDescriptor` has a single `ModelId` default but no way to constrain which models the agent can switch to.

This means the WebUI model dropdown shows models the user cannot actually use (no API key for that provider), and agents have unrestricted model switching.

---

## 2. Current Architecture (As-Is)

### Config Schema (`PlatformConfig.cs`)

```csharp
public Dictionary<string, ProviderConfig>? Providers { get; set; }

public sealed class ProviderConfig
{
    public string? ApiKey { get; set; }     // API key or "auth:copilot" reference
    public string? BaseUrl { get; set; }    // Base URL override
    public string? DefaultModel { get; set; } // Default model for this provider
}
```

**No `Enabled` flag. No `Models` allowlist.**

### Registration Flow (`Program.cs`)

```
BuiltInModels.RegisterAll(modelRegistry)
  → RegisterCopilotModels()    // 20 models
  → RegisterAnthropicModels()  // 4 models
  → RegisterOpenAIModels()     // 5 models
```

All 29 models registered unconditionally. No filtering.

### API Endpoints

- `ProvidersController.GetProviders()` — Returns all providers from `ModelRegistry.GetProviders()` (whatever providers have registered models).
- `ModelsController.GetModels()` — Returns all models from all providers.

### Agent Descriptor

```csharp
public required string ModelId { get; init; }       // Default model
public required string ApiProvider { get; init; }    // Required provider
// No AllowedModelIds or model restriction concept
```

### Auth Resolution (`GatewayAuthManager`)

Resolves API keys from: `auth.json` → environment variables → `PlatformConfig.Providers[].ApiKey`. Already has per-provider config lookup, but doesn't signal whether a provider is "active" or "authenticated."

---

## 3. Proposed Config Schema

### Provider Configuration (Layer 1)

Extend `ProviderConfig` with `Enabled` and `Models`:

```json
{
  "$schema": "https://botnexus.dev/schemas/config.v2.json",
  "version": 2,
  "providers": {
    "github-copilot": {
      "enabled": true,
      "models": [
        "claude-sonnet-4.5",
        "claude-sonnet-4.6",
        "gpt-5.2-codex",
        "gpt-5.4"
      ]
    },
    "anthropic": {
      "enabled": true,
      "apiKey": "sk-ant-...",
      "models": [
        "claude-sonnet-4-20250514",
        "claude-opus-4-5-20250929"
      ]
    },
    "openai": {
      "enabled": false
    }
  },
  "agents": {
    "coding-agent": {
      "provider": "github-copilot",
      "model": "claude-sonnet-4.5",
      "allowedModels": ["claude-sonnet-4.5", "claude-sonnet-4.6", "gpt-5.2-codex"],
      "systemPromptFile": "coding-agent.md",
      "toolIds": ["read", "write", "edit", "shell", "grep", "glob"]
    }
  }
}
```

### C# Schema Changes

```csharp
// ProviderConfig — existing class, new properties
public sealed class ProviderConfig
{
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string? DefaultModel { get; set; }

    // NEW: Whether this provider is active. Default true for backward compat.
    public bool Enabled { get; set; } = true;

    // NEW: Allowed model IDs. null/empty = all models for this provider.
    public List<string>? Models { get; set; }
}

// AgentDefinitionConfig — existing class, new property
public sealed class AgentDefinitionConfig
{
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? SystemPromptFile { get; set; }
    public List<string>? ToolIds { get; set; }
    public string? IsolationStrategy { get; set; }
    public bool Enabled { get; set; } = true;

    // NEW: Model IDs this agent is allowed to use.
    // null/empty = all of the provider's allowed models.
    public List<string>? AllowedModels { get; set; }
}
```

### AgentDescriptor Changes (`Gateway.Abstractions`)

```csharp
public sealed record AgentDescriptor
{
    // ... existing properties ...

    public required string ModelId { get; init; }       // Default model (unchanged)
    public required string ApiProvider { get; init; }    // Required provider (unchanged)

    // NEW: Allowed model IDs for this agent. Empty = unrestricted
    // (will be intersected with provider's allowed models at resolution time).
    public IReadOnlyList<string> AllowedModelIds { get; init; } = [];
}
```

---

## 4. Filtering Pipeline (3 Layers)

### Layer 1: Platform Config → Active Providers + Allowed Models

**Location:** New `ProviderModelFilter` service (in `BotNexus.Providers.Core` or `BotNexus.Gateway`)

**Logic:**
1. After `BuiltInModels.RegisterAll()` populates the full model registry, a new filtering step applies the platform config.
2. For each provider in the config:
   - If `enabled: false` → remove all models for that provider from the active set.
   - If `models` is non-null and non-empty → only keep the listed models; remove others.
   - If `models` is null or empty → keep all models (no restriction).
3. Providers NOT mentioned in the config → **keep active with all models** (backward compatible: no config = everything available).

**Design choice:** We do NOT mutate the `ModelRegistry` directly. Instead, we introduce a **`FilteredModelRegistry`** (or a `IModelFilter` interface) that wraps `ModelRegistry` and applies config-based filtering. This keeps the full registry intact for diagnostics/admin views, while the controllers/agents see only the filtered view.

```csharp
public interface IModelFilter
{
    IReadOnlyList<string> GetActiveProviders();
    IReadOnlyList<LlmModel> GetAllowedModels(string provider);
    LlmModel? GetModel(string provider, string modelId);
    bool IsProviderActive(string provider);
    bool IsModelAllowed(string provider, string modelId);
}
```

### Layer 2: API Endpoints → Return Only Active/Allowed

**ProvidersController** changes:
```csharp
// Before: _modelRegistry.GetProviders()
// After:  _modelFilter.GetActiveProviders()
```

**ModelsController** changes:
```csharp
// Before: iterates all providers, returns all models
// After:  iterates active providers, returns only allowed models

// NEW optional query param: ?agentId=coding-agent
// If provided, further filters to only the agent's allowed models (Layer 3)
```

**New endpoint** (optional but recommended):
```
GET /api/models?provider={providerId}         → allowed models for a provider
GET /api/models?agentId={agentId}             → allowed models for an agent
GET /api/models?provider={id}&agentId={id}    → intersection
```

### Layer 3: Per-Agent → Agent's AllowedModelIds

**Resolution logic** (in `AgentDescriptorValidator` or agent creation):

```
effectiveModels = providerAllowedModels                    // from Layer 1
if (agent.AllowedModelIds is not empty):
    effectiveModels = effectiveModels ∩ agent.AllowedModelIds  // intersection
```

**Validation rules:**
- `agent.ModelId` MUST be in the effective model set (or validation warning).
- `agent.AllowedModelIds` entries that don't exist in the provider's allowed models → validation warning (not error — graceful degradation).

**WebUI impact:** When the user opens the model dropdown for an agent, the UI calls `GET /api/models?agentId=X` and gets back only the models that agent is allowed to use.

---

## 5. Key Design Decisions

| Question | Decision | Rationale |
|----------|----------|-----------|
| `models: []` or omitting `models` | **All models** (no restriction) | Matches existing behavior. Empty array = "didn't configure" = "allow all." This is the safest backward-compat default. |
| `models: ["nonexistent"]` | **Validation warning, model ignored** | Don't hard-fail on typos. Log warning, skip unknown model IDs. |
| Omitting a provider from config entirely | **Provider stays active with all models** | Backward compat. Existing configs have no `providers` section. |
| Disabled providers' models | **Completely hidden** from API responses | Disabled = not available. Don't show grayed-out models — it confuses users and adds UI complexity. |
| Filter approach | **Wrapper/decorator**, not mutation | Don't modify `ModelRegistry` contents. Introduce `IModelFilter` that wraps it. Full registry stays available for admin/diagnostics. |
| `AllowedModelIds` empty on agent | **Unrestricted** (inherits provider's full list) | Consistent with `models` on provider. Empty = "all available." |
| Config version | **Bump to v2** when provider filtering ships | The `version` field exists for this purpose. v1 configs work unchanged. v2 adds the new fields. |
| Where `IModelFilter` lives | **`BotNexus.Providers.Core`** | It's a provider-concern abstraction. Controllers and agents depend on it via interface. |

---

## 6. Migration & Backward Compatibility

### Scenario: Existing config with no `providers` section

```json
{
  "version": 1,
  "gateway": { "listenUrl": "http://localhost:5005" }
}
```

**Behavior:** All providers active, all models available. Identical to current behavior. No migration required.

### Scenario: Existing config with `providers` but no `enabled`/`models`

```json
{
  "providers": {
    "copilot": { "apiKey": "auth:github-copilot" }
  }
}
```

**Behavior:** `Enabled` defaults to `true`. `Models` defaults to `null` (all models). Existing configs work unchanged.

### Scenario: User adds `enabled: false` to disable a provider

```json
{
  "providers": {
    "openai": { "enabled": false }
  }
}
```

**Behavior:** OpenAI provider and all its models disappear from API responses and WebUI dropdowns.

### Version Validation

- `PlatformConfigLoader.Validate()` already handles version warnings.
- New fields (`enabled`, `models`, `allowedModels`) are all nullable/defaulted — no breaking changes to the JSON schema.
- `PlatformConfigSchema` auto-generates from the C# types, so schema updates are automatic.

---

## 7. Validation Rules (New)

Add to `PlatformConfigLoader.ValidateProviders()`:

1. If `provider.Models` is non-null, each entry must be a non-empty string.
2. If `provider.Models` contains IDs not in `BuiltInModels` for that provider → **warning** (not error).
3. If `provider.Enabled` is `false` and `provider.Models` is non-null → **warning** (models list is ignored when disabled).

Add to `PlatformConfigLoader.ValidateAgents()`:

4. If `agent.AllowedModels` is non-null, each entry must be a non-empty string.
5. If `agent.Model` is not in `agent.AllowedModels` (when non-empty) → **warning** (default model not in allowed set).

Add to `AgentDescriptorValidator.Validate()`:

6. If `AllowedModelIds` is non-empty and `ModelId` is not in the list → **validation warning**.

---

## 8. Implementation Plan

### Phase A: Config Schema + Filtering Core (Farnsworth)

**Owner:** Farnsworth (owns `Providers.Core` and `Gateway.Configuration`)  
**Depends on:** This proposal being accepted.

1. Add `Enabled` and `Models` properties to `ProviderConfig`.
2. Add `AllowedModels` property to `AgentDefinitionConfig`.
3. Add `AllowedModelIds` property to `AgentDescriptor`.
4. Update `FileAgentConfigurationSource.AgentConfigurationFile` with `AllowedModels`.
5. Update `FileAgentConfigurationSource.BuildDescriptor()` to map `AllowedModels` → `AllowedModelIds`.
6. Update `PlatformConfigAgentSource` to map `AllowedModels` → `AllowedModelIds`.
7. Add validation rules (§7 items 1–6) to `PlatformConfigLoader` and `AgentDescriptorValidator`.
8. Update `PlatformConfigSchema` tests (auto-generated, but verify).

### Phase B: IModelFilter + Filtering Pipeline (Farnsworth)

**Owner:** Farnsworth  
**Depends on:** Phase A.

1. Create `IModelFilter` interface in `BotNexus.Providers.Core`.
2. Create `PlatformConfigModelFilter : IModelFilter` that wraps `ModelRegistry` + reads `PlatformConfig`.
3. Register `IModelFilter` in DI (`Program.cs`).
4. Update `ProvidersController` to inject `IModelFilter` instead of `ModelRegistry`.
5. Update `ModelsController` to inject `IModelFilter` instead of `ModelRegistry`.
6. Add optional `?agentId=` query parameter to `ModelsController` for Layer 3 filtering.
7. Wire `IModelFilter` into agent creation/validation path (intersection with agent's `AllowedModelIds`).

### Phase C: WebUI Updates (Fry)

**Owner:** Fry  
**Depends on:** Phase B (API contract changes).

1. Update provider dropdown to reflect only active providers.
2. Pass `agentId` parameter when fetching models for an existing agent.
3. Add `allowedModels` field to agent creation/edit form (multi-select or tag input).
4. Handle gracefully if the models endpoint returns fewer models than before.

### Phase D: Tests (Hermes)

**Owner:** Hermes  
**Depends on:** Phase A (can start schema tests immediately).

1. **Unit tests for `PlatformConfigModelFilter`:**
   - Provider enabled/disabled filtering.
   - Model allowlist filtering.
   - Omitted provider = active with all models.
   - Empty `models` array = all models.
   - Unknown model IDs in allowlist.
2. **Unit tests for `AgentDescriptor.AllowedModelIds`:**
   - Intersection with provider's allowed models.
   - Empty AllowedModelIds = unrestricted.
   - Default model not in allowed set → warning.
3. **Controller integration tests:**
   - `GET /api/providers` returns only active providers.
   - `GET /api/models` returns only allowed models.
   - `GET /api/models?agentId=X` returns agent-scoped models.
4. **Config validation tests:**
   - Backward compat: v1 config works unchanged.
   - New validation warnings fire correctly.
5. **E2E agent creation test:**
   - Agent with `allowedModels` restricts model switching.

---

## 9. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Breaking existing configs | Low | High | All new fields are nullable/defaulted. v1 configs work unchanged. |
| Performance (filtering on every request) | Low | Low | `IModelFilter` can cache its filtered view; invalidate on config reload. |
| Extension providers not filtered | Medium | Medium | Document that extension-registered providers are active by default; config can disable them by name. |
| WebUI regression | Medium | Medium | Fry tests with both v1 (no filtering) and v2 (filtered) configs. |

---

## 10. Open Questions

1. **Should `IModelFilter` react to hot-reload?** — The `PlatformConfigLoader.Watch()` mechanism already exists. `PlatformConfigModelFilter` should subscribe and rebuild its cached filtered view on config changes. This is the expected behavior.

2. **Should the admin API (`/api/admin/models`) show the full unfiltered list?** — Probably yes, for diagnostics. But this is a future enhancement, not required in Phase A/B.

3. **Should `AllowedModelIds` on the agent be validated against the provider's model set at load time?** — Yes, as warnings. Hard errors would break configs when models are renamed upstream.

---

## Appendix: Example Configurations

### Minimal (backward compatible — no changes needed)

```json
{
  "version": 1,
  "gateway": {
    "listenUrl": "http://localhost:5005",
    "defaultAgentId": "assistant"
  }
}
```

### Copilot-Only Setup

```json
{
  "version": 2,
  "providers": {
    "github-copilot": {
      "enabled": true,
      "models": ["claude-sonnet-4.5", "gpt-5.2-codex", "gpt-5.4"]
    },
    "anthropic": { "enabled": false },
    "openai": { "enabled": false }
  },
  "agents": {
    "coding-agent": {
      "provider": "github-copilot",
      "model": "claude-sonnet-4.5",
      "allowedModels": ["claude-sonnet-4.5", "gpt-5.2-codex"],
      "systemPromptFile": "coding-agent.md",
      "toolIds": ["read", "write", "edit", "shell", "grep", "glob"]
    }
  }
}
```

### Multi-Provider with Restrictions

```json
{
  "version": 2,
  "providers": {
    "github-copilot": {
      "enabled": true,
      "models": ["claude-sonnet-4.5", "gpt-5.4"]
    },
    "anthropic": {
      "enabled": true,
      "apiKey": "auth:anthropic",
      "models": ["claude-sonnet-4-20250514"]
    }
  },
  "agents": {
    "coding-agent": {
      "provider": "github-copilot",
      "model": "claude-sonnet-4.5",
      "allowedModels": ["claude-sonnet-4.5"]
    },
    "research-agent": {
      "provider": "anthropic",
      "model": "claude-sonnet-4-20250514"
    }
  }
}
```
