using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using BotNexus.Channels.Base;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway;

/// <summary>
/// The main gateway hosted service. Starts all channels and dispatches
/// inbound messages from the message bus to registered agent runners.
/// </summary>
public sealed class Gateway : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly ChannelManager _channelManager;
    private readonly IEnumerable<IAgentRunner> _agentRunners;
    private readonly ILogger<Gateway> _logger;
    private readonly BotNexusConfig _config;

    public Gateway(
        IMessageBus messageBus,
        ChannelManager channelManager,
        IEnumerable<IAgentRunner> agentRunners,
        ILogger<Gateway> logger,
        IOptions<BotNexusConfig> config)
    {
        _messageBus = messageBus;
        _channelManager = channelManager;
        _agentRunners = agentRunners;
        _logger = logger;
        _config = config.Value;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BotNexus Gateway starting...");

        await _channelManager.StartAllAsync(stoppingToken).ConfigureAwait(false);

        _logger.LogInformation("BotNexus Gateway ready. Listening for messages...");

        await foreach (var message in _messageBus.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            // Dispatch concurrently to all agent runners
            var runners = _agentRunners.ToList();
            if (runners.Count == 0)
            {
                _logger.LogWarning("No agent runners registered, dropping message");
                continue;
            }

            // Run the first matching runner (could be extended for per-agent routing)
            _ = Task.Run(async () =>
            {
                try
                {
                    await runners[0].RunAsync(message, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from {Channel}/{ChatId}",
                        message.Channel, message.ChatId);
                }
            }, stoppingToken);
        }

        _logger.LogInformation("BotNexus Gateway stopping...");
        await _channelManager.StopAllAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
