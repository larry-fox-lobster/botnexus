using Microsoft.JSInterop;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

/// <summary>
/// Toggleable feature flags backed by browser localStorage.
/// Flags are read on first access and can be changed at runtime via:
///   localStorage.setItem('bn:feature:{flagName}', 'true')
/// then hard-refresh.
/// </summary>
public sealed class FeatureFlagsService
{
    private readonly IJSRuntime _js;
    private readonly Dictionary<string, bool> _cache = new();
    private bool _initialized;

    public FeatureFlagsService(IJSRuntime js) => _js = js;

    /// <summary>
    /// Cache conversation history in localStorage for instant render on revisit.
    /// Default: false. Enable with localStorage.setItem('bn:feature:conversationHistoryCache', 'true')
    /// </summary>
    public bool ConversationHistoryCache => Get("conversationHistoryCache");

    /// <summary>Loads all flags from localStorage. Call once in OnInitializedAsync.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        // Load known flags
        await LoadFlag("conversationHistoryCache");
    }

    private bool Get(string flag) =>
        _cache.TryGetValue(flag, out var v) ? v : false;

    private async Task LoadFlag(string flag)
    {
        try
        {
            var val = await _js.InvokeAsync<string?>("localStorage.getItem", $"bn:feature:{flag}");
            _cache[flag] = string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            _cache[flag] = false;
        }
    }
}
