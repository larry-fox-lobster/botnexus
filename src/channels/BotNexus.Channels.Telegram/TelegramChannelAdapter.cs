using BotNexus.Gateway.Abstractions.Channels;
using BotNexus.Gateway.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace BotNexus.Channels.Telegram;

/// <summary>
/// Telegram Bot channel adapter.
/// </summary>
/// <remarks>
/// Phase 2 stub: this implementation only tracks lifecycle state and logs outbound
/// sends. A full implementation would connect to Telegram Bot API using long polling
/// or webhooks, map inbound updates to <see cref="InboundMessage"/>, and dispatch them
/// through <see cref="IChannelDispatcher"/>.
/// </remarks>
public sealed class TelegramChannelAdapter(
    ILogger<TelegramChannelAdapter> logger,
    TelegramOptions options) : IChannelAdapter
{
    private readonly ILogger<TelegramChannelAdapter> _logger = logger;
    private readonly TelegramOptions _options = options;
    private IChannelDispatcher? _dispatcher;
    private bool _isRunning;

    /// <summary>
    /// Gets the channel type identifier.
    /// </summary>
    public string ChannelType => "telegram";

    /// <summary>
    /// Gets the human-readable channel display name.
    /// </summary>
    public string DisplayName => "Telegram Bot";

    /// <summary>
    /// Gets a value indicating whether this channel supports streaming deltas.
    /// </summary>
    /// <remarks>
    /// Telegram Bot API is message-based and does not support delta streaming natively.
    /// </remarks>
    public bool SupportsStreaming => false;

    /// <summary>
    /// Gets a value indicating whether the adapter is running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Starts the Telegram adapter stub and stores the dispatcher callback.
    /// </summary>
    /// <param name="dispatcher">Dispatcher used for routing inbound messages to the gateway.</param>
    /// <param name="cancellationToken">Cancellation token for startup operations.</param>
    /// <returns>A completed task once startup state is recorded.</returns>
    /// <remarks>
    /// Phase 2 stub: full implementation would connect to Telegram Bot API via long polling or webhook.
    /// </remarks>
    public Task StartAsync(IChannelDispatcher dispatcher, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _dispatcher = dispatcher;
        _isRunning = true;
        _logger.LogInformation(
            "{DisplayName} channel adapter stub started (WebhookUrlConfigured: {WebhookUrlConfigured}, AllowedChatCount: {AllowedChatCount})",
            DisplayName,
            !string.IsNullOrWhiteSpace(_options.WebhookUrl),
            _options.AllowedChatIds.Count);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the Telegram adapter stub.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for shutdown operations.</param>
    /// <returns>A completed task once shutdown state is recorded.</returns>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_dispatcher is null && !_isRunning)
        {
            return Task.CompletedTask;
        }

        _isRunning = false;
        _dispatcher = null;
        _logger.LogInformation("{DisplayName} channel adapter stub stopped", DisplayName);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a complete outbound message through the Telegram adapter stub.
    /// </summary>
    /// <param name="message">Outbound message payload.</param>
    /// <param name="cancellationToken">Cancellation token for send operations.</param>
    /// <returns>A completed task after logging the outbound send intent.</returns>
    /// <remarks>
    /// Phase 2 stub: full implementation would call Telegram <c>sendMessage</c> API.
    /// </remarks>
    public Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "{DisplayName} stub send to conversation {ConversationId}: {Content}",
            DisplayName,
            message.ConversationId,
            message.Content);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a streaming delta send request.
    /// </summary>
    /// <param name="conversationId">Target conversation identifier.</param>
    /// <param name="delta">Streaming content delta.</param>
    /// <param name="cancellationToken">Cancellation token for send operations.</param>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// Phase 2 stub: streaming is not supported for Telegram.
    /// </remarks>
    public Task SendStreamDeltaAsync(string conversationId, string delta, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Full implementation would ignore this path or translate it into message edits when supported.
        return Task.CompletedTask;
    }
}
