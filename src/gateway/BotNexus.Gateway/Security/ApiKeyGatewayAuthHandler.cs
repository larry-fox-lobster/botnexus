using BotNexus.Gateway.Abstractions.Security;
using Microsoft.Extensions.Logging;

namespace BotNexus.Gateway.Security;

/// <summary>
/// API key authentication handler for Gateway HTTP and WebSocket requests.
/// </summary>
/// <remarks>
/// <para>
/// If no API key is configured, authentication runs in development mode and allows all requests.
/// </para>
/// <para>
/// If a key is configured, callers must provide either <c>Authorization: Bearer {key}</c>
/// or <c>X-Api-Key: {key}</c>.
/// </para>
/// </remarks>
public sealed class ApiKeyGatewayAuthHandler : IGatewayAuthHandler
{
    private const string AuthorizationHeader = "Authorization";
    private const string ApiKeyHeader = "X-Api-Key";
    private const string BearerPrefix = "Bearer ";

    private readonly string? _configuredApiKey;
    private readonly ILogger<ApiKeyGatewayAuthHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyGatewayAuthHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ApiKeyGatewayAuthHandler(ILogger<ApiKeyGatewayAuthHandler> logger)
        : this(null, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyGatewayAuthHandler"/> class.
    /// </summary>
    /// <param name="apiKey">Configured API key. Null or empty enables development mode.</param>
    /// <param name="logger">Logger instance.</param>
    public ApiKeyGatewayAuthHandler(string? apiKey, ILogger<ApiKeyGatewayAuthHandler> logger)
    {
        _configuredApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Scheme => "ApiKey";

    /// <inheritdoc />
    public Task<GatewayAuthResult> AuthenticateAsync(
        GatewayAuthContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_configuredApiKey is null)
        {
            _logger.LogDebug("Gateway auth is running in development mode: no API key configured.");
            return Task.FromResult(GatewayAuthResult.Success(new GatewayCallerIdentity
            {
                CallerId = "gateway-dev",
                DisplayName = "Gateway Development Caller",
                IsAdmin = true
            }));
        }

        var presentedKey = ExtractApiKey(context.Headers);
        if (presentedKey is null)
            return Task.FromResult(GatewayAuthResult.Failure("Missing API key."));

        if (!string.Equals(presentedKey, _configuredApiKey, StringComparison.Ordinal))
            return Task.FromResult(GatewayAuthResult.Failure("Invalid API key."));

        return Task.FromResult(GatewayAuthResult.Success(new GatewayCallerIdentity
        {
            CallerId = "gateway-api-key",
            DisplayName = "Gateway API Key Caller",
            IsAdmin = true
        }));
    }

    private static string? ExtractApiKey(IReadOnlyDictionary<string, string> headers)
    {
        if (TryGetHeaderValue(headers, ApiKeyHeader, out var apiKeyHeaderValue) &&
            !string.IsNullOrWhiteSpace(apiKeyHeaderValue))
        {
            return apiKeyHeaderValue.Trim();
        }

        if (!TryGetHeaderValue(headers, AuthorizationHeader, out var authorizationValue))
            return null;

        if (string.IsNullOrWhiteSpace(authorizationValue) ||
            !authorizationValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorizationValue[BearerPrefix.Length..].Trim();
        return token.Length > 0 ? token : null;
    }

    private static bool TryGetHeaderValue(
        IReadOnlyDictionary<string, string> headers,
        string headerName,
        out string value)
    {
        if (headers.TryGetValue(headerName, out value!))
            return true;

        foreach (var (key, candidateValue) in headers)
        {
            if (string.Equals(key, headerName, StringComparison.OrdinalIgnoreCase))
            {
                value = candidateValue;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
