---
status: deferred
depends-on: Wave 2 (MessageRole, SessionType smart enums)
created: 2026-04-12
---

# Phase 9.1: Unify SystemPromptBuilder

## Summary

Decompose the Gateway's 572-line `SystemPromptBuilder.Build()` into a pipeline of testable section builders. Extract shared prompt primitives into a new `BotNexus.Prompts` project. Enable both Gateway and CodingAgent to compose prompts from shared sections without depending on each other.

## Why Deferred

Large refactor independent of the core DDD alignment. Needs MessageRole smart enum (delivered in Wave 2) and snapshot tests of current output BEFORE any changes.

## Current State

### Gateway SystemPromptBuilder (572 lines)
- `BotNexus.Gateway/Agents/SystemPromptBuilder.cs`
- Static class with one massive `Build()` method
- 15+ internal helper methods (buildToolsSection, buildSkillsSection, buildMessagingSection, etc.)
- Takes `SystemPromptParams` record with 20+ fields
- Outputs a single concatenated string
- Context file ordering, cache boundaries, channel-specific logic all inline

### CodingAgent SystemPromptBuilder (283 lines)
- `BotNexus.CodingAgent/SystemPromptBuilder.cs`
- Separate static class with its own `Build()` method
- Takes `SystemPromptContext` record
- Different parameter types: `PromptContextFile` vs `ContextFile`, `ToolPromptContribution` (unique)
- Simpler - no channels, no skills, no heartbeat

### Shared Concepts
Both builders handle:
- Context file loading and ordering
- Tool name injection
- Environment/runtime info
- Custom/override prompts
- System prompt concatenation

## Target State

### BotNexus.Prompts (new project)

A shared library for prompt composition. Neither Gateway nor CodingAgent - sits alongside BotNexus.Domain as a shared foundation. AgentCore stays lean and does NOT absorb this.

```
BotNexus.Prompts/
    IPromptSection.cs           - interface for a prompt section builder
    PromptPipeline.cs           - orchestrates section builders into a final prompt
    PromptContext.cs             - shared context bag that sections read from
    Sections/
        ToolSection.cs           - tool availability and guidelines
        SafetySection.cs         - safety rules
        ExecutionBiasSection.cs  - action-oriented behavior rules
        ContextFileSection.cs    - workspace context file injection
        RuntimeSection.cs        - runtime info line
    ContextFileOrdering.cs       - shared ordering logic for workspace files
```

### Section Builder Interface

```csharp
public interface IPromptSection
{
    /// Order in the pipeline (lower = earlier in prompt)
    int Order { get; }

    /// Whether this section should be included given the current context
    bool ShouldInclude(PromptContext context);

    /// Build the section's lines
    IReadOnlyList<string> Build(PromptContext context);
}
```

### PromptPipeline

```csharp
public sealed class PromptPipeline
{
    private readonly List<IPromptSection> _sections = [];

    public PromptPipeline Add(IPromptSection section) { _sections.Add(section); return this; }

    public string Build(PromptContext context)
    {
        var lines = _sections
            .Where(s => s.ShouldInclude(context))
            .OrderBy(s => s.Order)
            .SelectMany(s => s.Build(context))
            .ToList();
        return string.Join("\n", lines);
    }
}
```

### PromptContext

Shared bag of data that sections read from:

```csharp
public sealed record PromptContext
{
    public required string WorkspaceDir { get; init; }
    public IReadOnlyList<ContextFile> ContextFiles { get; init; } = [];
    public IReadOnlySet<ToolName> AvailableTools { get; init; } = new HashSet<ToolName>();
    public bool IsMinimal { get; init; }
    public string? Channel { get; init; }
    public IDictionary<string, object?> Extensions { get; init; } = new Dictionary<string, object?>();
    // Extensions bag lets Gateway and CodingAgent add their own context without BotNexus.Prompts knowing about it
}
```

### Gateway Composition

Gateway adds its own sections on top of the shared ones:

```csharp
var pipeline = new PromptPipeline()
    // Shared sections from BotNexus.Prompts
    .Add(new ToolSection())
    .Add(new SafetySection())
    .Add(new ExecutionBiasSection())
    .Add(new ContextFileSection())
    .Add(new RuntimeSection())
    // Gateway-specific sections
    .Add(new SkillsSection())
    .Add(new MessagingSection())
    .Add(new ReplyTagsSection())
    .Add(new VoiceSection())
    .Add(new HeartbeatSection())
    .Add(new SilentReplySection())
    .Add(new CacheBoundarySection())
    .Add(new GatewayCliSection());

return pipeline.Build(context);
```

### CodingAgent Composition

```csharp
var pipeline = new PromptPipeline()
    .Add(new ToolSection())
    .Add(new SafetySection())
    .Add(new ContextFileSection())
    .Add(new RuntimeSection())
    // CodingAgent-specific
    .Add(new EnvironmentSection())      // git branch, package manager
    .Add(new ToolGuidelinesSection());   // tool-specific contributions

return pipeline.Build(context);
```

### What Moves Where

| Current (Gateway) | Target | Project |
|-------------------|--------|---------|
| `buildToolsSection` logic | `ToolSection` | BotNexus.Prompts |
| Safety rules | `SafetySection` | BotNexus.Prompts |
| Execution bias rules | `ExecutionBiasSection` | BotNexus.Prompts |
| Context file ordering/injection | `ContextFileSection` + `ContextFileOrdering` | BotNexus.Prompts |
| Runtime line | `RuntimeSection` | BotNexus.Prompts |
| Skills section | `SkillsSection` | BotNexus.Gateway |
| Messaging section | `MessagingSection` | BotNexus.Gateway |
| Reply tags | `ReplyTagsSection` | BotNexus.Gateway |
| Voice/TTS | `VoiceSection` | BotNexus.Gateway |
| Heartbeat | `HeartbeatSection` | BotNexus.Gateway |
| Silent reply | `SilentReplySection` | BotNexus.Gateway |
| Cache boundary | `CacheBoundarySection` | BotNexus.Gateway |
| CLI reference | `GatewayCliSection` | BotNexus.Gateway |
| Approval guidance | Part of `ToolSection` or `ApprovalSection` | BotNexus.Gateway |

## Snapshot Tests (Required BEFORE Refactoring)

Before any code changes, capture the exact prompt output for:
1. Full mode with all features (tools, skills, context files, heartbeat, voice)
2. Minimal mode (sub-agent prompts)
3. No tools mode
4. CodingAgent prompt with git context and tool contributions

These snapshots become the regression tests. After the refactor, the pipeline must produce identical output for the same inputs.

## Migration Plan

1. **Write snapshot tests** for both SystemPromptBuilders
2. **Create `BotNexus.Prompts` project** with `IPromptSection`, `PromptPipeline`, `PromptContext`
3. **Extract shared sections** one at a time (start with SafetySection - simplest, no dependencies)
4. **Replace Gateway builder** to use the pipeline internally (output must match snapshots)
5. **Replace CodingAgent builder** to use shared sections where applicable
6. **Delete old static builders** once both consumers use the pipeline
7. **Verify snapshots pass** at every step

## Risks

1. **Prompt sensitivity**: LLMs are sensitive to exact prompt wording. Even whitespace changes can affect behavior. Snapshot tests are critical.
2. **Section ordering**: The current builder has implicit ordering. The pipeline makes ordering explicit via `Order` property, but initial values must match current output exactly.
3. **Conditional sections**: Some sections depend on others (e.g., messaging section references tool names). The `PromptContext.Extensions` bag handles this but needs careful coordination.

## Acceptance Criteria

- [ ] `BotNexus.Prompts` project exists with section pipeline
- [ ] Gateway uses pipeline, produces identical output to current builder (snapshot verified)
- [ ] CodingAgent uses shared sections where applicable
- [ ] Each section is independently testable
- [ ] Old static SystemPromptBuilder classes are deleted
- [ ] Neither Gateway nor CodingAgent depends on the other
- [ ] AgentCore is not touched
