using System.Text.Json;
using BotNexus.Channels.Base;
using BotNexus.Core.Abstractions;
using BotNexus.Core.Bus;
using BotNexus.Core.Configuration;
using BotNexus.Core.Extensions;
using BotNexus.Providers.Base;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway.HealthChecks;

public sealed class MessageBusHealthCheck(IMessageBus messageBus) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var healthy = messageBus is not MessageBus concreteBus || concreteBus.IsAlive;
        return Task.FromResult(healthy
            ? HealthCheckResult.Healthy("Message bus is alive")
            : HealthCheckResult.Unhealthy("Message bus is not accepting messages"));
    }
}

public sealed class ProviderRegistrationHealthCheck(ProviderRegistry providerRegistry) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var providers = providerRegistry.GetProviderNames();
        return Task.FromResult(providers.Count > 0
            ? HealthCheckResult.Healthy("At least one provider is registered", data: new Dictionary<string, object> { ["providers"] = providers })
            : HealthCheckResult.Unhealthy("No providers are registered"));
    }
}

public sealed class ExtensionLoaderHealthCheck(ExtensionLoadReport loadReport) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (loadReport.CompletedSuccessfully)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Extension loader completed successfully",
                data: new Dictionary<string, object>
                {
                    ["loaded"] = loadReport.LoadedCount,
                    ["failed"] = loadReport.FailedCount
                }));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("Extension loader reported failures",
            data: new Dictionary<string, object>
            {
                ["loaded"] = loadReport.LoadedCount,
                ["failed"] = loadReport.FailedCount
            }));
    }
}

public sealed class ChannelReadinessHealthCheck(ChannelManager channelManager, IOptions<BotNexusConfig> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var enabledChannels = options.Value.Channels.Instances
            .Where(pair => pair.Value.Enabled)
            .Select(pair => pair.Key)
            .ToList();

        if (enabledChannels.Count == 0)
            return Task.FromResult(HealthCheckResult.Healthy("No enabled channels configured"));

        var notReady = enabledChannels
            .Where(name => channelManager.GetChannel(name)?.IsRunning != true)
            .ToList();

        if (notReady.Count == 0)
            return Task.FromResult(HealthCheckResult.Healthy("All configured channels are running"));

        return Task.FromResult(HealthCheckResult.Unhealthy(
            $"Channels not ready: {string.Join(", ", notReady)}",
            data: new Dictionary<string, object> { ["notReadyChannels"] = notReady }));
    }
}

public sealed class ProviderReadinessHealthCheck(ProviderRegistry providerRegistry, IOptions<BotNexusConfig> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var configuredProviders = options.Value.Providers.Keys.ToList();
        var registeredProviders = providerRegistry.GetProviderNames();
        var missingProviders = configuredProviders
            .Where(name => !registeredProviders.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missingProviders.Count == 0)
            return Task.FromResult(HealthCheckResult.Healthy("All configured providers are initialized"));

        return Task.FromResult(HealthCheckResult.Unhealthy(
            $"Providers not ready: {string.Join(", ", missingProviders)}",
            data: new Dictionary<string, object> { ["missingProviders"] = missingProviders }));
    }
}

public static class HealthCheckJsonResponseWriter
{
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    duration = entry.Value.Duration.TotalMilliseconds,
                    data = entry.Value.Data
                }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
