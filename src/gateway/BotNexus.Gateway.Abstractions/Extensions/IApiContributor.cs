using Microsoft.AspNetCore.Routing;

namespace BotNexus.Gateway.Abstractions.Extensions;

/// <summary>
/// Contributes to the gateway's shared API surface.
/// Receives a scoped RouteGroupBuilder pre-namespaced to prevent route collisions.
/// </summary>
public interface IApiContributor
{
    /// <summary>
    /// Registers API endpoints within the provided scoped route group
    /// (e.g., /api/extensions/{extensionId}/).
    /// </summary>
    void MapApiRoutes(RouteGroupBuilder apiGroup);
}
