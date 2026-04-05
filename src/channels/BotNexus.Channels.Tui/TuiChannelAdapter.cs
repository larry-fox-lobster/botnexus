using BotNexus.Gateway.Abstractions.Channels;
using BotNexus.Gateway.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace BotNexus.Channels.Tui;

/// <summary>
/// Terminal UI channel adapter for local console I/O.
/// </summary>
/// <remarks>
/// Phase 2 stub: this implementation only tracks lifecycle state and writes outbound
/// content to <see cref="Console.Out"/>. A full implementation would run a background
/// input loop, translate console input into <see cref="InboundMessage"/> instances,
/// and dispatch them through the registered <see cref="IChannelDispatcher"/>.
/// </remarks>
public sealed class TuiChannelAdapter(ILogger<TuiChannelAdapter> logger) : IChannelAdapter
{
    private readonly ILogger<TuiChannelAdapter> _logger = logger;
    private IChannelDispatcher? _dispatcher;
    private bool _isRunning;

    /// <summary>
    /// Gets the channel type identifier.
    /// </summary>
    public string ChannelType => "tui";

    /// <summary>
    /// Gets the human-readable channel display name.
    /// </summary>
    public string DisplayName => "Terminal UI";

    /// <summary>
    /// Gets a value indicating whether this channel supports streaming deltas.
    /// </summary>
    public bool SupportsStreaming => true;

    /// <summary>
    /// Gets a value indicating whether the adapter is running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Starts the adapter and stores the dispatcher callback for future inbound dispatch.
    /// </summary>
    /// <param name="dispatcher">Dispatcher used for routing inbound messages to the gateway.</param>
    /// <param name="cancellationToken">Cancellation token for startup operations.</param>
    /// <returns>A completed task once startup state is recorded.</returns>
    /// <remarks>
    /// Phase 2 stub: no stdin read loop is started yet.
    /// </remarks>
    public Task StartAsync(IChannelDispatcher dispatcher, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _dispatcher = dispatcher;
        _isRunning = true;
        _logger.LogInformation("{DisplayName} channel adapter stub started", DisplayName);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the adapter.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for shutdown operations.</param>
    /// <returns>A completed task once shutdown state is recorded.</returns>
    /// <remarks>
    /// Phase 2 stub: a full implementation would also stop any background stdin listener.
    /// </remarks>
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
    /// Sends a complete outbound message to the terminal.
    /// </summary>
    /// <param name="message">Outbound message to render.</param>
    /// <param name="cancellationToken">Cancellation token for send operations.</param>
    /// <returns>A task that completes when the message has been written.</returns>
    /// <remarks>
    /// Phase 2 stub: writes directly to stdout. A full implementation would route output
    /// through structured terminal rendering components.
    /// </remarks>
    public Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_isRunning || _dispatcher is null)
        {
            _logger.LogDebug("{DisplayName} send requested while adapter is not running", DisplayName);
        }

        return Console.Out.WriteLineAsync($"[{DisplayName}:{message.ConversationId}] {message.Content}");
    }

    /// <summary>
    /// Sends a streaming delta to the terminal without appending a newline.
    /// </summary>
    /// <param name="conversationId">Target conversation identifier.</param>
    /// <param name="delta">Streaming text delta.</param>
    /// <param name="cancellationToken">Cancellation token for send operations.</param>
    /// <returns>A task that completes when the delta has been written.</returns>
    /// <remarks>
    /// Phase 2 stub: writes deltas directly to stdout for quick validation of streaming paths.
    /// </remarks>
    public Task SendStreamDeltaAsync(string conversationId, string delta, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_isRunning || _dispatcher is null)
        {
            _logger.LogDebug("{DisplayName} stream delta requested while adapter is not running", DisplayName);
        }

        return Console.Out.WriteAsync(delta);
    }
}
