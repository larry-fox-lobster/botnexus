# Agent Projects Rules

## Dependency boundary

Projects in `src/agent/` must **never** depend on projects outside this folder. The agent layer is self-contained — it defines the core agent runtime and LLM provider abstractions. The gateway, extensions, and other layers depend on the agent layer, not the other way around.

**Allowed dependencies:**
- Other projects within `src/agent/` (e.g., providers reference `Agent.Providers.Core`)
- NuGet packages

**Prohibited dependencies:**
- `src/gateway/` — the agent layer does not know about the gateway
- `src/extensions/` — extensions depend on agents, not vice versa
- `src/common/` — if agent needs shared utilities, they belong in `Agent.Core`
- `src/domain/` — domain primitives are consumed via NuGet-style or must be pulled into Agent.Core if needed

## Project structure

| Project | Purpose |
|---------|---------|
| `BotNexus.Agent.Core` | Core agent runtime — loop, tools, hooks, types, configuration |
| `BotNexus.Agent.Providers.Core` | Shared provider abstractions — `LlmClient`, models, streaming |
| `BotNexus.Agent.Providers.OpenAI` | OpenAI Responses API provider |
| `BotNexus.Agent.Providers.Anthropic` | Anthropic Messages API provider |
| `BotNexus.Agent.Providers.Copilot` | GitHub Copilot provider |
| `BotNexus.Agent.Providers.OpenAICompat` | OpenAI-compatible endpoint provider (vLLM, SGLang, etc.) |

## Adding a new provider

1. Create `src/agent/BotNexus.Agent.Providers.{Name}/`
2. Reference only `BotNexus.Agent.Providers.Core` (sibling project)
3. Implement `ILlmProvider` or extend `LlmClient`
4. Do not reference gateway, extension, or domain projects
