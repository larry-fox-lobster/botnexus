using System.Text.Json;
using Microsoft.JSInterop;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

/// <summary>
/// Caches conversation history in browser localStorage to allow instant render
/// on revisit. Entries expire after 5 minutes. Gated by
/// <see cref="FeatureFlagsService.ConversationHistoryCache"/>.
/// </summary>
public sealed class ConversationHistoryCache
{
    private readonly IJSRuntime _js;

    /// <summary>Maximum age of a cache entry before it is considered stale.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Initializes the cache with the given JS runtime for localStorage access.</summary>
    public ConversationHistoryCache(IJSRuntime js) => _js = js;

    /// <summary>
    /// Returns cached history for <paramref name="conversationId"/> if a fresh entry exists,
    /// or null when the cache is empty, expired, or unreadable.
    /// </summary>
    public async Task<CachedHistory?> GetAsync(string conversationId)
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", CacheKey(conversationId));
            if (json is null) return null;

            var entry = JsonSerializer.Deserialize<CachedHistory>(json, JsonOptions);
            if (entry is null) return null;

            // Treat entries older than MaxAge as stale
            if (DateTimeOffset.UtcNow - entry.LoadedAt > MaxAge)
                return null;

            return entry;
        }
        catch
        {
            // JS interop failures (e.g. private browsing with storage blocked) are non-fatal
            return null;
        }
    }

    /// <summary>
    /// Serialises <paramref name="messages"/> and writes them to localStorage under the
    /// cache key for <paramref name="conversationId"/>, stamped with the current UTC time.
    /// Silently swallows JS interop failures (e.g. private browsing with storage blocked).
    /// </summary>
    public async Task SetAsync(string conversationId, IReadOnlyList<ChatMessage> messages)
    {
        try
        {
            var entry = new CachedHistory(conversationId, DateTimeOffset.UtcNow, [.. messages]);
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await _js.InvokeVoidAsync("localStorage.setItem", CacheKey(conversationId), json);
        }
        catch
        {
            // Non-fatal — caching is best-effort
        }
    }

    /// <summary>
    /// Removes the localStorage entry for <paramref name="conversationId"/>, preventing
    /// stale history from appearing after a session reset.
    /// </summary>
    public async Task InvalidateAsync(string conversationId)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", CacheKey(conversationId));
        }
        catch
        {
            // Non-fatal
        }
    }

    private static string CacheKey(string id) => $"bn:conv-history:{id}";
}

/// <summary>
/// Snapshot of cached conversation history retrieved from localStorage.
/// </summary>
public sealed record CachedHistory(string ConversationId, DateTimeOffset LoadedAt, List<ChatMessage> Messages);
