using BotNexus.Core.Configuration;
using Microsoft.Extensions.Options;

namespace BotNexus.Gateway;

public sealed class ApiKeyAuthenticationMiddleware
{
    private static readonly object _errorBody = new
    {
        error = "Unauthorized",
        message = "Invalid or missing API key."
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly string? _configuredApiKey;
    private readonly bool _isApiKeyConfigured;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        IOptions<BotNexusConfig> config,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _configuredApiKey = config.Value.Gateway.ApiKey;
        _isApiKeyConfigured = !string.IsNullOrWhiteSpace(_configuredApiKey);

        if (!_isApiKeyConfigured)
        {
            _logger.LogWarning(
                "BotNexus:Gateway:ApiKey is not configured. Allowing unauthenticated gateway access.");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_isApiKeyConfigured)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var providedApiKey = context.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrWhiteSpace(providedApiKey))
            providedApiKey = context.Request.Query["apiKey"].ToString();

        if (!string.Equals(providedApiKey, _configuredApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(_errorBody, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }
}
