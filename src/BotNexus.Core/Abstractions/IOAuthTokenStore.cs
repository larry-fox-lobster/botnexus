namespace BotNexus.Core.Abstractions;

public interface IOAuthTokenStore
{
    Task<OAuthToken?> LoadTokenAsync(string providerName, CancellationToken cancellationToken = default);
    Task SaveTokenAsync(string providerName, OAuthToken token, CancellationToken cancellationToken = default);
    Task ClearTokenAsync(string providerName, CancellationToken cancellationToken = default);
}
