using Microsoft.AspNetCore.Builder;

namespace BotNexus.Gateway.Abstractions.Extensions;

/// <summary>
/// Extension-owned endpoints, static files, middleware, and transport surfaces.
/// Called during app startup after WebApplication is built.
/// </summary>
public interface IEndpointContributor
{
    /// <summary>
    /// Registers extension-owned endpoints (hubs, webhooks), static files, and middleware.
    /// </summary>
    void MapEndpoints(WebApplication app);
}
