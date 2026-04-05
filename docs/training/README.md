# BotNexus Training Guide

Learn how the BotNexus agent system works — from LLM providers to the agent loop — so you can build your own coding-agent implementations.

## What This Training Covers

BotNexus is a modular AI agent execution platform built in C#/.NET. This guide walks through every layer of the architecture:

| Doc | Topic | You'll Learn |
|-----|-------|-------------|
| [01 — Architecture Overview](01-architecture-overview.md) | System architecture | How the layers connect, project structure, dependency flow |
| [02 — Provider System](02-provider-system.md) | LLM providers | Streaming protocol, message types, provider registry |
| [03 — Agent Core](03-agent-core.md) | The agent loop | State management, tool execution, hooks, events |
| [04 — Coding Agent](04-coding-agent.md) | Building a coding agent | Factory, tools, sessions, extensions, safety |
| [05 — Build Your Own Agent](05-building-your-own.md) | Tutorial | Step-by-step: custom agent, tool, and extension |
| [06 — Add a Provider](06-adding-a-provider.md) | Tutorial | Step-by-step: implement a new LLM provider |

## Prerequisites

- **C# / .NET 10** — You should be comfortable with records, async/await, `IAsyncEnumerable`, and `System.Threading.Channels`.
- **LLM API concepts** — Familiarity with chat completions, streaming (SSE), tool calling, and system prompts.
- **Git** — The codebase uses Git for version control and session management.

## Recommended Reading Order

**If you're building a custom agent:**
1. [Architecture Overview](01-architecture-overview.md) — Get the big picture
2. [Agent Core](03-agent-core.md) — Understand the loop
3. [Build Your Own Agent](05-building-your-own.md) — Hands-on tutorial

**If you're adding a new LLM provider:**
1. [Architecture Overview](01-architecture-overview.md) — Get the big picture
2. [Provider System](02-provider-system.md) — Understand the streaming protocol
3. [Add a Provider](06-adding-a-provider.md) — Hands-on tutorial

**If you're building extensions or tools:**
1. [Architecture Overview](01-architecture-overview.md) — Get the big picture
2. [Agent Core](03-agent-core.md) — Understand tool execution and hooks
3. [Coding Agent](04-coding-agent.md) — See how built-in tools work
4. [Build Your Own Agent](05-building-your-own.md) — Tool and extension tutorials

**Full deep dive:** Read 01 through 06 in order.

## Glossary

| Term | Definition |
|------|-----------|
| **Agent** | Stateful wrapper that manages a conversation loop — prompt → LLM → tools → repeat |
| **AgentLoop** | The inner execution cycle: send context to LLM, accumulate response, execute tools, repeat until done |
| **ContentBlock** | A typed chunk of message content — text, thinking, image, or tool call |
| **Context** | The full payload sent to an LLM: system prompt + message history + tool definitions |
| **Extension** | A plugin that hooks into the agent lifecycle (tool calls, sessions, compaction) |
| **Hook** | A callback invoked before or after tool execution — for validation, logging, or result transformation |
| **LlmClient** | Top-level entry point that routes LLM requests to the correct provider |
| **LlmStream** | An async enumerable of streaming events from an LLM response |
| **Message** | A conversation turn — `UserMessage`, `AssistantMessage`, or `ToolResultMessage` |
| **ModelRegistry** | Registry of available LLM models with metadata (context window, pricing, capabilities) |
| **Provider** | An `IApiProvider` implementation that talks to a specific LLM API (Anthropic, OpenAI, etc.) |
| **Skill** | A markdown file with instructions injected into the system prompt to teach the agent new capabilities |
| **StopReason** | Why the LLM stopped generating — `Stop`, `ToolUse`, `Length`, `Error`, etc. |
| **StreamAccumulator** | Converts streaming events into a complete `AssistantMessage` |
| **Tool** | An `IAgentTool` implementation the agent can invoke — read files, run commands, etc. |
| **ToolExecutor** | Runs tool calls (sequential or parallel) with hook orchestration |
| **Turn** | One LLM invocation + optional tool execution within an agent run |

## Project Repository Structure

```
src/
├── providers/
│   ├── BotNexus.Providers.Core/       # Models, streaming, registry
│   ├── BotNexus.Providers.Anthropic/  # Anthropic Claude provider
│   ├── BotNexus.Providers.OpenAI/     # OpenAI provider
│   ├── BotNexus.Providers.Copilot/    # GitHub Copilot provider
│   └── BotNexus.Providers.OpenAICompat/ # OpenAI-compatible endpoints
├── agent/
│   └── BotNexus.AgentCore/            # Agent loop, tools, hooks, state
└── coding-agent/
    └── BotNexus.CodingAgent/          # Coding agent factory, tools, sessions
```
