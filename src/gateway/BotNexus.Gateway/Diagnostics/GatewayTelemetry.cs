using System.Diagnostics.Metrics;

namespace BotNexus.Gateway.Diagnostics;

public static class GatewayTelemetry
{
    public const string MeterName = "BotNexus.Gateway";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "botnexus.gateway.requests",
        unit: "{request}",
        description: "Incoming gateway requests.");

    public static readonly Counter<long> MessagesProcessed = Meter.CreateCounter<long>(
        "botnexus.gateway.messages.processed",
        unit: "{message}",
        description: "Inbound messages processed by the gateway.");

    public static readonly UpDownCounter<long> ActiveSessions = Meter.CreateUpDownCounter<long>(
        "botnexus.gateway.sessions.active",
        unit: "{session}",
        description: "Active session count observed by the gateway.");

    public static readonly Histogram<double> AgentExecutionDurationMs = Meter.CreateHistogram<double>(
        "botnexus.gateway.agent.execution.duration",
        unit: "ms",
        description: "Agent execution duration in milliseconds.");

    public static readonly Histogram<double> ProviderLatencyMs = Meter.CreateHistogram<double>(
        "botnexus.gateway.provider.latency",
        unit: "ms",
        description: "Provider-facing latency observed while executing agent prompts.");
}
