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

        // Serve static files for /blazor/ — must be UseStaticFiles (middleware)
        // so it runs before endpoint routing catches the fallback.
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = blazorFileProvider,
            RequestPath = "/blazor"
        });

        // SPA fallback — only for paths that don't match a physical file.
        // Static file middleware above handles JS/CSS/WASM; this catches
        // client-side routes like /blazor/some-page.
        var indexBytes = File.ReadAllBytes(Path.Combine(blazorPath, "index.html"));
        app.MapFallback("/blazor/{**path}", context =>
        {
            // Don't serve index.html for requests that look like static files
            // (they would have been handled by UseStaticFiles if they existed)
            var path = context.Request.Path.Value ?? "";
            if (path.Contains('.'))
            {
                context.Response.StatusCode = 404;
                return Task.CompletedTask;
            }

            context.Response.ContentType = "text/html";
            return context.Response.Body.WriteAsync(indexBytes).AsTask();
        });
    }
}
