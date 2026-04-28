using System.Net.Http.Json;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

public sealed class GatewayInfoService
{
    private readonly HttpClient _http;
    public GatewayInfo? Info { get; private set; }

    public GatewayInfoService(HttpClient http) => _http = http;

    public async Task LoadAsync()
    {
        try
        {
            Info = await _http.GetFromJsonAsync<GatewayInfo>("api/gateway/info");
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
