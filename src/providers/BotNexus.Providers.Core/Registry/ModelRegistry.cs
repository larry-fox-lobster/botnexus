using System.Collections.Concurrent;
using BotNexus.Providers.Core.Models;

namespace BotNexus.Providers.Core.Registry;

/// <summary>
/// Global model registry. Port of pi-mono's models.ts registry.
/// Thread-safe via ConcurrentDictionary.
/// </summary>
public static class ModelRegistry
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, LlmModel>> _registry = new();

    public static void Register(string provider, LlmModel model)
    {
        var models = _registry.GetOrAdd(provider, _ => new ConcurrentDictionary<string, LlmModel>());
        models[model.Id] = model;
    }

    public static LlmModel? GetModel(string provider, string modelId)
    {
        if (_registry.TryGetValue(provider, out var models) &&
            models.TryGetValue(modelId, out var model))
            return model;

        return null;
    }

    public static IReadOnlyList<string> GetProviders()
    {
        return _registry.Keys.ToList();
    }

    public static IReadOnlyList<LlmModel> GetModels(string provider)
    {
        return _registry.TryGetValue(provider, out var models)
            ? models.Values.ToList()
            : [];
    }

    /// <summary>
    /// Calculate cost from usage and model pricing.
    /// Port of pi-mono's calculateCost from models.ts.
    /// </summary>
    public static UsageCost CalculateCost(LlmModel model, Usage usage)
    {
        const decimal perMillion = 1_000_000m;
        var input = usage.Input * model.Cost.Input / perMillion;
        var output = usage.Output * model.Cost.Output / perMillion;
        var cacheRead = usage.CacheRead * model.Cost.CacheRead / perMillion;
        var cacheWrite = usage.CacheWrite * model.Cost.CacheWrite / perMillion;
        var total = input + output + cacheRead + cacheWrite;
        return new UsageCost(input, output, cacheRead, cacheWrite, total);
    }

    public static void Clear()
    {
        _registry.Clear();
    }
}
