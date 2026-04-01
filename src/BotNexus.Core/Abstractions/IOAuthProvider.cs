namespace BotNexus.Core.Abstractions;

public interface IOAuthProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    bool HasValidToken { get; }
}
