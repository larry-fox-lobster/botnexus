using System.Text.Json;
using BotNexus.Core.Abstractions;

namespace BotNexus.Providers.Copilot;

public sealed class FileOAuthTokenStore(string? tokenDirectory = null) : IOAuthTokenStore
{
    private readonly string _tokenDirectory = tokenDirectory
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".botnexus",
            "tokens");

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
    }

    public Task ClearTokenAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var tokenFile = GetTokenFile(providerName);
        if (File.Exists(tokenFile))
            File.Delete(tokenFile);

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
