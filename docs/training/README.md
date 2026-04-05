# BotNexus Training Documentation

Learn how BotNexus works internally and how to build your own coding agent on top of it.

## Who Is This For?

Developers who want to understand BotNexus internals — the provider system, agent loop, streaming pipeline, and tool execution model — and potentially build their own AI coding agent.

## Sections

| # | Document | What You'll Learn |
|---|----------|-------------------|
| 0 | [Architecture Overview](00-overview.md) | Project structure, how the pieces fit together |
| 1 | [Provider System](01-provider-system.md) | IApiProvider, model registry, API key resolution, request flow |
| 2 | [Streaming](02-streaming.md) | LlmStream, SSE parsing, event types, StreamAccumulator |
| 3 | [Agent Loop](03-agent-loop.md) | Agent lifecycle, AgentLoopRunner, turn mechanics, steering |
| 4 | [Tool Execution](04-tool-execution.md) | IAgentTool, sequential vs parallel, hooks, custom tools |
| 5 | [CodingAgent Layer](05-coding-agent.md) | Built-in tools, system prompts, sessions, extensions |
| 6 | [Building Your Own](06-building-your-own.md) | Step-by-step guide to creating a custom agent |

## Suggested Reading Order

If you're new to BotNexus, read the docs in order — each builds on the previous. If you're looking for something specific:

- **"How does a request flow from user to LLM?"** → Start with [01-provider-system.md](01-provider-system.md)
- **"How does streaming work?"** → [02-streaming.md](02-streaming.md)
- **"What happens in the agent loop?"** → [03-agent-loop.md](03-agent-loop.md)
- **"How do I add a custom tool?"** → [04-tool-execution.md](04-tool-execution.md)
- **"How do I build my own agent?"** → [06-building-your-own.md](06-building-your-own.md)

## Prerequisites

- Familiarity with C# and .NET (async/await, records, interfaces)
- Basic understanding of LLM APIs (chat completions, tool calling, streaming)
- The BotNexus source code checked out locally

## Related Documentation

- [Getting Started Guide](../getting-started.md)
- [Architecture Reference](../architecture.md)
- [Extension Development](../extension-development.md)
- [Configuration Reference](../configuration.md)
