# Farnsworth Channel Stub Decisions

- Date: 2026-04-06
- Owner: Farnsworth
- Requester: Jon Bullen (Copilot)

## Decisions

1. Added two new channel projects under `src/channels/`:
   - `BotNexus.Channels.Tui`
   - `BotNexus.Channels.Telegram`
2. Implemented both adapters directly against `IChannelAdapter` as Phase 2 stubs with explicit lifecycle state (`IsRunning`) and minimal outbound behavior.
3. Marked TUI as streaming-capable (`SupportsStreaming = true`) and Telegram as non-streaming (`SupportsStreaming = false`) to match protocol behavior.
4. Added DI extension methods for both channels:
   - `AddBotNexusTuiChannel(IServiceCollection)`
   - `AddBotNexusTelegramChannel(IServiceCollection, Action<TelegramOptions>? configure = null)`
5. Added `TelegramOptions` with `BotToken`, `WebhookUrl`, and `AllowedChatIds` to reserve contract surface for full Telegram Bot API integration.
6. Added both new projects to `BotNexus.slnx` and validated individual project builds and solution build.

## Follow-up for Full Implementation

- TUI: add background stdin reader loop and dispatch inbound messages via `IChannelDispatcher`.
- Telegram: add long-polling/webhook receiver, map updates to `InboundMessage`, and call Bot API `sendMessage`/edit endpoints.
