using System.Text.Json;
using Microsoft.JSInterop;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

/// <summary>
/// Caches conversation history in browser localStorage so revisited conversations
/// render instantly without a server round-trip.
/// Cache key: bn:conv-history:{conversationId}
/// </summary>
public sealed class ConversationHistoryCache
{
    private readonly IJSRuntime _js;
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    public ConversationHistoryCache(IJSRuntime js) => _js = js;

    public async Task<CachedHistory?> GetAsync(string conversationId)
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", CacheKey(conversationId));
            if (string.IsNullOrEmpty(json)) return null;
            var cached = JsonSerializer.Deserialize<CachedHistory>(json);
            if (cached is null) return null;
            // Expire stale cache
            if (DateTimeOffset.UtcNow - cached.LoadedAt > MaxAge) return null;
            return cached;
        }
        catch { return null; }
    }

    public async Task SetAsync(string conversationId, List<ChatMessage> messages)
    {
        try
        {
            var entry = new CachedHistory(conversationId, DateTimeOffset.UtcNow, messages);
            var json = JsonSerializer.Serialize(entry);
            await _js.InvokeVoidAsync("localStorage.setItem", CacheKey(conversationId), json);
        }
        catch { /* non-fatal */ }
    }

    public async Task InvalidateAsync(string conversationId)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", CacheKey(conversationId));
        }
        catch { /* non-fatal */ }
    }

    private static string CacheKey(string conversationId) => $"bn:conv-history:{conversationId}";
}

public sealed record CachedHistory(
    string ConversationId,
    DateTimeOffset LoadedAt,
    List<ChatMessage> Messages);
