using BotNexus.Gateway.Abstractions.Extensions;
using Microsoft.AspNetCore.Builder;

namespace BotNexus.Channels.SignalR;

/// <summary>
/// Registers the SignalR hub and static file serving for the WebUI.
/// </summary>
public class SignalREndpointContributor : IEndpointContributor
{
    public void MapEndpoints(WebApplication app)
    {
        // Register SignalR hub
        app.MapHub<GatewayHub>("/hub/gateway");
    }
}
