using System.Text.Json;
using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotNexus.Providers.Copilot;

public sealed class FileOAuthTokenStore : IOAuthTokenStore
{
    private readonly string _tokenDirectory;
    private readonly ILogger<FileOAuthTokenStore> _logger;

    // Constructor for DI (recommended)
    public FileOAuthTokenStore(ILogger<FileOAuthTokenStore> logger)
        : this(tokenDirectory: null, logger)
    {
    }

    // Constructor for manual instantiation with optional parameters
    public FileOAuthTokenStore(string? tokenDirectory = null, ILogger<FileOAuthTokenStore>? logger = null)
    {
        _tokenDirectory = tokenDirectory
            ?? Path.Combine(
                BotNexusHome.ResolveHomePath(),
                "tokens");
        _logger = logger ?? NullLogger<FileOAuthTokenStore>.Instance;
    }

    public async Task<OAuthToken?> LoadTokenAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var tokenFile = GetTokenFile(providerName);
        if (!File.Exists(tokenFile))
            return null;

        var json = await File.ReadAllTextAsync(tokenFile, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<OAuthToken>(json);
    }

    public async Task SaveTokenAsync(string providerName, OAuthToken token, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_tokenDirectory);
        var tokenFile = GetTokenFile(providerName);
        var json = JsonSerializer.Serialize(token);
        await File.WriteAllTextAsync(tokenFile, json, cancellationToken).ConfigureAwait(false);
        
        _logger.LogWarning("OAuth token saved for provider '{ProviderName}' at {TokenFile}. Expires at {ExpiresAt:u}",
            providerName, tokenFile, token.ExpiresAt);
    }

    public Task ClearTokenAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var tokenFile = GetTokenFile(providerName);
        if (File.Exists(tokenFile))
        {
            _logger.LogWarning("Clearing OAuth token for provider '{ProviderName}' at {TokenFile}", providerName, tokenFile);
            File.Delete(tokenFile);
        }
        else
        {
            _logger.LogInformation("No token file to clear for provider '{ProviderName}' at {TokenFile}", providerName, tokenFile);
        }

        return Task.CompletedTask;
    }

    private string GetTokenFile(string providerName)
    {
        var fileName = string.Join(
            "_",
            providerName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return Path.Combine(_tokenDirectory, $"{fileName}.json");
    }
}
