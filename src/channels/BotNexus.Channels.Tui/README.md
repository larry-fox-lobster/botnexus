# BotNexus.Channels.Tui

> Terminal UI channel adapter for local console interaction.

## Overview

This package provides a terminal/console channel adapter for the BotNexus Gateway. It derives from `ChannelAdapterBase` and writes outbound messages directly to `Console.Out`. It's intended for local development and testing of the Gateway message pipeline without requiring external services.

**Status: Stub** — Output works. The inbound input loop is not yet implemented.

## Key Types

| Type | Kind | Description |
|------|------|-------------|
| `TuiChannelAdapter` | Class | Terminal UI adapter. Writes outbound messages and streaming deltas to stdout. |
| `TuiServiceCollectionExtensions` | Static class | DI registration extension method `AddBotNexusTuiChannel()`. |

## Current Capabilities

| Feature | Status | Notes |
|---------|--------|-------|
| Outbound messages | ✅ Working | Writes `[Terminal UI:{conversationId}] {content}` to stdout |
| Streaming deltas | ✅ Working | Writes deltas to stdout without a newline |
| Inbound input loop | ❌ Planned | Console input → `InboundMessage` → `DispatchInboundAsync` |
| Structured rendering | ❌ Planned | Markdown rendering, syntax highlighting, progress indicators |

### What It Does Now

- Registers as channel type `"tui"` with display name `"Terminal UI"`
- Reports `SupportsStreaming = true`
- On `StartAsync`: logs that the stub has started
- On `SendAsync`: writes `[Terminal UI:{conversationId}] {content}` to `Console.Out`
- On `SendStreamDeltaAsync`: writes the delta text to `Console.Out` (no newline)
- On `StopAsync`: logs that the stub has stopped

### What's Planned

- Background `Console.ReadLine` loop that reads user input and dispatches it as `InboundMessage` instances
- Structured terminal rendering (Markdown, code blocks, progress bars)
- Conversation management (session selection, history display)

## Usage

### Registration

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBotNexusTuiChannel();
```

### Local Testing

The TUI adapter is useful for verifying the outbound message pipeline without a WebSocket client or external channel:

1. Register the TUI channel in your Gateway host
2. Send a message via the REST API or WebSocket targeting the `"tui"` channel
3. Observe the output on the console

## Configuration

No configuration options. The adapter writes directly to `Console.Out`.

## Dependencies

- **Target framework:** `net10.0`
- **Project references:**
  - `BotNexus.Gateway.Abstractions` — `IChannelAdapter`, `IChannelDispatcher`, message models
  - `BotNexus.Channels.Core` — `ChannelAdapterBase`
- **NuGet packages:**
  - `Microsoft.Extensions.DependencyInjection.Abstractions` — DI registration

## Extension Points

This is a concrete adapter, not a base class. To customize terminal behavior:

- Fork this adapter and add your own rendering logic
- Or implement a new adapter deriving from `ChannelAdapterBase` with a different channel type (e.g., `"rich-tui"`)
