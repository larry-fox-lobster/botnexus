using BotNexus.Gateway.Abstractions.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace BotNexus.Extensions.Channels.SignalR;

/// <summary>
/// Registers the SignalR hub and Blazor WASM client hosting.
/// All web surface for this channel is self-contained in this extension.
/// </summary>
public class SignalREndpointContributor : IEndpointContributor
{
    public void MapEndpoints(WebApplication app)
    {
        app.MapHub<GatewayHub>("/hub/gateway");

        var extensionDir = Path.GetDirectoryName(typeof(SignalREndpointContributor).Assembly.Location)!;
        var blazorPath = Path.Combine(extensionDir, "blazor");

        if (!Directory.Exists(blazorPath))
            return;

        var blazorFileProvider = new PhysicalFileProvider(blazorPath);
        var indexBytes = File.ReadAllBytes(Path.Combine(blazorPath, "index.html"));

        // Serve Blazor WASM client at the root URL.
        // API, hub, health, and swagger paths are excluded so they reach their own handlers.
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";

            // Let API, hub, health, and swagger requests pass through
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/hub/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/health", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            // Try to serve a static file from the Blazor output
            var subPath = path == "/" ? "/index.html" : path;
            var fileInfo = blazorFileProvider.GetFileInfo(subPath);

            if (fileInfo.Exists && !fileInfo.IsDirectory)
            {
                var contentType = GetContentType(subPath);
                context.Response.ContentType = contentType;
                context.Response.ContentLength = fileInfo.Length;
                await using var stream = fileInfo.CreateReadStream();
                await stream.CopyToAsync(context.Response.Body);
                return;
            }

            // SPA fallback for client-side routes (paths without file extensions)
            if (!subPath.Contains('.'))
            {
                context.Response.ContentType = "text/html";
                await context.Response.Body.WriteAsync(indexBytes);
                return;
            }

            // Fall through for non-Blazor requests or missing files
            await next();
        });
    }

    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html",
        ".css" => "text/css",
        ".js" => "application/javascript",
        ".json" => "application/json",
        ".wasm" => "application/wasm",
        ".dll" => "application/octet-stream",
        ".pdb" => "application/octet-stream",
        ".dat" => "application/octet-stream",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".ico" => "image/x-icon",
        ".map" => "application/json",
        ".gz" => "application/gzip",
        ".br" => "application/brotli",
        _ => "application/octet-stream"
    };
}
