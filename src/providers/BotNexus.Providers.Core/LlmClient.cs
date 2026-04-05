using BotNexus.Providers.Core.Models;
using BotNexus.Providers.Core.Registry;
using BotNexus.Providers.Core.Streaming;

namespace BotNexus.Providers.Core;

/// <summary>
/// Top-level streaming functions. Port of pi-mono's stream.ts.
/// Resolves the API provider from the registry and delegates.
/// </summary>
public sealed class LlmClient
{
    public ApiProviderRegistry ApiProviders { get; }
    public ModelRegistry Models { get; }

    public LlmClient(ApiProviderRegistry apiProviderRegistry, ModelRegistry modelRegistry)
    {
        ApiProviders = apiProviderRegistry ?? throw new ArgumentNullException(nameof(apiProviderRegistry));
        Models = modelRegistry ?? throw new ArgumentNullException(nameof(modelRegistry));
    }

    public LlmStream Stream(LlmModel model, Context context, StreamOptions? options = null)
    {
        var provider = ResolveProvider(model.Api);
        return provider.Stream(model, context, options);
    }

    public async Task<AssistantMessage> CompleteAsync(
        LlmModel model, Context context, StreamOptions? options = null)
    {
        var stream = Stream(model, context, options);
        return await stream.GetResultAsync();
    }

    public LlmStream StreamSimple(LlmModel model, Context context, SimpleStreamOptions? options = null)
    {
        var provider = ResolveProvider(model.Api);
        return provider.StreamSimple(model, context, options);
    }

    public async Task<AssistantMessage> CompleteSimpleAsync(
        LlmModel model, Context context, SimpleStreamOptions? options = null)
    {
        var stream = StreamSimple(model, context, options);
        return await stream.GetResultAsync();
    }

    private IApiProvider ResolveProvider(string api)
    {
        return ApiProviders.Get(api)
               ?? throw new InvalidOperationException($"No API provider registered for api: {api}");
    }
}
