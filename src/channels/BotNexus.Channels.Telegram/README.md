# BotNexus.Channels.Telegram

> Telegram Bot channel adapter for the BotNexus Gateway.

## Overview

This package provides a Telegram Bot channel adapter that connects the BotNexus Gateway to Telegram's messaging platform. It derives from `ChannelAdapterBase` and supports configuration for bot tokens, webhook URLs, and chat ID allow-lists.

**Status: Stub** — Lifecycle management and logging are implemented. Telegram Bot API integration (sending/receiving messages) is not yet wired up.

## Key Types

| Type | Kind | Description |
|------|------|-------------|
| `TelegramChannelAdapter` | Class | Telegram bot adapter. Logs outbound sends; inbound message handling is pending. |
| `TelegramOptions` | Class | Configuration options — bot token, webhook URL, and allowed chat IDs. |
| `TelegramServiceCollectionExtensions` | Static class | DI registration extension method `AddBotNexusTelegramChannel()`. |

## Current Capabilities

| Feature | Status | Notes |
|---------|--------|-------|
| Lifecycle management | ✅ Working | Start/stop with configuration logging |
| Outbound sends | 🔶 Stub | Logs send intent; does not call Telegram API |
| Inbound messages | ❌ Planned | Long polling or webhook → `InboundMessage` → dispatch |
| Streaming deltas | ❌ N/A | Telegram is message-based; `SupportsStreaming = false` |
| Chat ID allow-list | ✅ Configured | `AllowedChatIds` in options (enforcement pending with inbound) |

### What It Does Now

- Registers as channel type `"telegram"` with display name `"Telegram Bot"`
- Reports `SupportsStreaming = false` (Telegram is message-based, not streaming)
- On `StartAsync`: logs startup with webhook URL status and allowed chat count
- On `SendAsync`: logs the outbound message content and conversation ID
- On `StopAsync`: logs shutdown

### What's Planned

- Telegram Bot API client integration (via `BotToken`)
- Long polling or webhook mode for receiving inbound updates
- Mapping Telegram updates to `InboundMessage` and dispatching through `IChannelDispatcher`
- Chat ID allow-list enforcement on inbound messages
- `sendMessage` API calls for outbound delivery
- Message edit support for pseudo-streaming (editing the last message as content arrives)

## Usage

### Registration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBotNexusTelegramChannel(options =>
{
    options.BotToken = builder.Configuration["Telegram:BotToken"];
    options.WebhookUrl = builder.Configuration["Telegram:WebhookUrl"];
    options.AllowedChatIds.Add(123456789);  // Restrict to specific chats
});
```

### Configuration via appsettings.json

```json
{
  "Telegram": {
    "BotToken": "your-bot-token-here",
    "WebhookUrl": "https://your-domain.com/api/telegram/webhook"
  }
}
```

## Configuration

| Option | Type | Description |
|--------|------|-------------|
| `BotToken` | `string?` | Telegram Bot API token from [@BotFather](https://t.me/botfather). Required for API calls. |
| `WebhookUrl` | `string?` | Public URL for webhook mode. If unset, the adapter would use long polling. |
| `AllowedChatIds` | `ICollection<long>` | Telegram chat IDs allowed to interact with this bot. Empty allows all chats. |

## Dependencies

- **Target framework:** `net10.0`
- **Project references:**
  - `BotNexus.Gateway.Abstractions` — `IChannelAdapter`, `IChannelDispatcher`, message models
  - `BotNexus.Channels.Core` — `ChannelAdapterBase`
- **NuGet packages:**
  - `Microsoft.Extensions.DependencyInjection.Abstractions` — DI registration
  - `Microsoft.Extensions.Options` — `IOptions<TelegramOptions>` binding

## Extension Points

This is a concrete adapter. To customize Telegram behavior:

- Configure `TelegramOptions` via DI to set bot token, webhook URL, and allowed chats
- When the full implementation lands, extend by overriding or decorating the adapter's message mapping

## Reference

- [Telegram Bot API documentation](https://core.telegram.org/bots/api)
- [BotFather — creating a new bot](https://t.me/botfather)
