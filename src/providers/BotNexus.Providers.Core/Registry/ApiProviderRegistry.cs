using System.Collections.Concurrent;

namespace BotNexus.Providers.Core.Registry;

/// <summary>
/// Global registry of API providers. Port of pi-mono's api-registry.ts.
/// Thread-safe via ConcurrentDictionary.
/// </summary>
public static class ApiProviderRegistry
{
    private sealed record Registration(IApiProvider Provider, string? SourceId);

    private static readonly ConcurrentDictionary<string, Registration> _registry = new();

    public static void Register(IApiProvider provider, string? sourceId = null)
    {
        _registry[provider.Api] = new Registration(provider, sourceId);
    }

    public static IApiProvider? Get(string api)
    {
        return _registry.TryGetValue(api, out var reg) ? reg.Provider : null;
    }

    public static IReadOnlyList<IApiProvider> GetAll()
    {
        return _registry.Values.Select(r => r.Provider).ToList();
    }

    public static void Unregister(string sourceId)
    {
        var toRemove = _registry
            .Where(kvp => kvp.Value.SourceId == sourceId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var api in toRemove)
            _registry.TryRemove(api, out _);
    }

    public static void Clear()
    {
        _registry.Clear();
    }
}
