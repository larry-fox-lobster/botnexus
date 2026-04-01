using System.Diagnostics.Metrics;
using System.Threading;

namespace BotNexus.Core.Observability;

public sealed class BotNexusMetrics : IBotNexusMetrics, IDisposable
{
    private readonly Meter _meter = new("BotNexus.Platform", "1.0.0");
    private readonly Counter<long> _messagesProcessed;
    private readonly Counter<long> _toolCallsExecuted;
    private readonly Histogram<double> _providerLatency;
    private readonly ObservableGauge<int> _extensionsLoaded;
    private int _loadedExtensions;

    public BotNexusMetrics()
    {
        _messagesProcessed = _meter.CreateCounter<long>("botnexus.messages.processed");
        _toolCallsExecuted = _meter.CreateCounter<long>("botnexus.tool_calls.executed");
        _providerLatency = _meter.CreateHistogram<double>("botnexus.provider.latency", unit: "ms");
        _extensionsLoaded = _meter.CreateObservableGauge<int>("botnexus.extensions.loaded",
            () => Volatile.Read(ref _loadedExtensions));
    }

    public void IncrementMessagesProcessed(string channel)
        => _messagesProcessed.Add(1, new KeyValuePair<string, object?>("channel", channel));

    public void IncrementToolCallsExecuted(string toolName)
        => _toolCallsExecuted.Add(1, new KeyValuePair<string, object?>("tool", toolName));

    public void RecordProviderLatency(string providerName, double elapsedMilliseconds)
        => _providerLatency.Record(elapsedMilliseconds, new KeyValuePair<string, object?>("provider", providerName));

    public void UpdateExtensionsLoaded(int loadedCount)
        => Interlocked.Exchange(ref _loadedExtensions, loadedCount);

    public void Dispose()
    {
        _meter.Dispose();
    }
}
