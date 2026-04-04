namespace BotNexus.Core.Abstractions;

public sealed record OAuthToken(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string? RefreshToken = null)
{
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}
