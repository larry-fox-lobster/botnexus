using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace BotNexus.Heartbeat;

/// <summary>
/// Heartbeat service that periodically records system health and runs as an IHostedService.
/// </summary>
public sealed class HeartbeatService : BackgroundService, IHeartbeatService
{
    private readonly ILogger<HeartbeatService> _logger;
    private readonly HeartbeatConfig _config;
    private readonly AgentDefaults _agentDefaults;
    private readonly IMemoryConsolidator _memoryConsolidator;
    private readonly IAgentWorkspaceFactory _workspaceFactory;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastConsolidationUtc = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _lastBeat;

    public HeartbeatService(
        ILogger<HeartbeatService> logger,
        IOptions<BotNexusConfig> config,
        IMemoryConsolidator memoryConsolidator,
        IAgentWorkspaceFactory workspaceFactory)
    {
        _logger = logger;
        var botNexusConfig = config.Value;
        _config = botNexusConfig.Gateway.Heartbeat;
        _agentDefaults = botNexusConfig.Agents;
        _memoryConsolidator = memoryConsolidator;
        _workspaceFactory = workspaceFactory;
    }

    /// <inheritdoc/>
    public void Beat()
    {
        _lastBeat = DateTimeOffset.UtcNow;
        _logger.LogDebug("Heartbeat recorded at {Time}", _lastBeat);
    }

    /// <inheritdoc/>
    public DateTimeOffset? LastBeat => _lastBeat;

    /// <inheritdoc/>
    public bool IsHealthy =>
        _lastBeat.HasValue &&
        DateTimeOffset.UtcNow - _lastBeat.Value < TimeSpan.FromSeconds(_config.IntervalSeconds * 2);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Heartbeat service disabled");
            return;
        }

        _logger.LogInformation("Heartbeat service started, interval: {Interval}s", _config.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            Beat();
            _logger.LogInformation("💓 BotNexus heartbeat at {Time}", _lastBeat);
            await RunConsolidationTriggersAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(_config.IntervalSeconds), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunConsolidationTriggersAsync(CancellationToken cancellationToken)
    {
        if (_agentDefaults.Named.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        foreach (var (agentName, agentConfig) in _agentDefaults.Named.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (agentConfig.EnableMemory != true)
                continue;

            var intervalHours = ResolveConsolidationIntervalHours(agentConfig.MemoryConsolidationIntervalHours);
            if (_lastConsolidationUtc.TryGetValue(agentName, out var lastConsolidation) &&
                now - lastConsolidation < TimeSpan.FromHours(intervalHours))
            {
                continue;
            }

            await RunAgentConsolidationAsync(agentName, now, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunAgentConsolidationAsync(string agentName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            var heartbeatInstructions = await LoadHeartbeatInstructionsAsync(agentName, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(heartbeatInstructions))
            {
                _logger.LogInformation(
                    "Loaded HEARTBEAT.md periodic instructions for {AgentName}: {Instructions}",
                    agentName,
                    heartbeatInstructions);
            }

            var result = await _memoryConsolidator.ConsolidateAsync(agentName, cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                _logger.LogInformation(
                    "Memory consolidation succeeded for {AgentName}. Daily files processed: {DailyFilesProcessed}, entries consolidated: {EntriesConsolidated}",
                    agentName,
                    result.DailyFilesProcessed,
                    result.EntriesConsolidated);
            }
            else
            {
                _logger.LogWarning(
                    "Memory consolidation failed for {AgentName}. Error: {Error}. Daily files processed: {DailyFilesProcessed}",
                    agentName,
                    result.Error ?? "unknown",
                    result.DailyFilesProcessed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled memory consolidation failure for {AgentName}", agentName);
        }
        finally
        {
            _lastConsolidationUtc[agentName] = now;
        }
    }

    private async Task<string?> LoadHeartbeatInstructionsAsync(string agentName, CancellationToken cancellationToken)
    {
        var workspace = _workspaceFactory.Create(agentName);
        await workspace.InitializeAsync(cancellationToken).ConfigureAwait(false);

        if (!workspace.FileExists("HEARTBEAT.md"))
            return null;

        var content = await workspace.ReadFileAsync("HEARTBEAT.md", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var instructions = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Where(static line => !line.StartsWith('#'))
            .Where(static line => !line.StartsWith("<!--", StringComparison.Ordinal) && !line.StartsWith("-->", StringComparison.Ordinal))
            .Take(5)
            .ToArray();

        return instructions.Length == 0 ? null : string.Join(" | ", instructions);
    }

    private static int ResolveConsolidationIntervalHours(int configuredIntervalHours)
        => configuredIntervalHours > 0 ? configuredIntervalHours : 24;
}
