using BotNexus.Gateway.Abstractions.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

namespace BotNexus.Channels.SignalR;

/// <summary>
/// Registers the SignalR hub and Blazor WASM client hosting.
/// All web surface for this channel is self-contained in this extension.
/// </summary>
public class SignalREndpointContributor : IEndpointContributor
{
    public void MapEndpoints(WebApplication app)
    {
        app.MapHub<GatewayHub>("/hub/gateway");

        // Blazor files are co-located with this extension assembly
        var extensionDir = Path.GetDirectoryName(typeof(SignalREndpointContributor).Assembly.Location)!;
        var blazorPath = Path.Combine(extensionDir, "blazor");
        if (!Directory.Exists(blazorPath))
            return;

        var blazorFileProvider = new PhysicalFileProvider(blazorPath);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = blazorFileProvider,
            RequestPath = "/blazor"
        });

        // SPA fallback — serve index.html for client-side routes
        var indexBytes = File.ReadAllBytes(Path.Combine(blazorPath, "index.html"));
        app.MapFallback("/blazor/{**path}", context =>
        {
            context.Response.ContentType = "text/html";
            return context.Response.Body.WriteAsync(indexBytes).AsTask();
        });
    }
}
