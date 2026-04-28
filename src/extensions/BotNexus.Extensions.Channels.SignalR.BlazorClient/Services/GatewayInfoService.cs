using System.Net.Http.Json;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

/// <summary>
/// Fetches build and runtime info from the gateway's /api/gateway/info endpoint.
/// Uses the hub-derived API base URL so it works correctly behind reverse proxies.
/// </summary>
public sealed class GatewayInfoService
{
    private readonly HttpClient _http;
    private readonly AgentSessionManager _manager;
    public GatewayInfo? Info { get; private set; }

    public GatewayInfoService(HttpClient http, AgentSessionManager manager)
    {
        _http = http;
        _manager = manager;
    }

    public async Task LoadAsync()
    {
        try
        {
            // Use the hub-derived API base URL — resolves correctly even behind
            // reverse proxies like NetBird where HostEnvironment.BaseAddress differs
            // from the actual gateway address.
            var baseUrl = _manager.ApiBaseUrl;
            if (string.IsNullOrEmpty(baseUrl)) return;

            Info = await _http.GetFromJsonAsync<GatewayInfo>($"{baseUrl}gateway/info");
        }
        catch { /* non-fatal — portal works without it */ }
    }
}

public sealed record GatewayInfo(
    DateTimeOffset StartedAt,
    long UptimeSeconds,
    string CommitSha,
    string CommitShort,
    string Version);
