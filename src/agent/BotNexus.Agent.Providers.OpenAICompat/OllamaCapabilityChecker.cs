using BotNexus.Agent.Providers.Core.Compatibility;
using System.Net.Http.Json;

namespace BotNexus.Agent.Providers.OpenAICompat;

/// <summary>
/// Queries the Ollama /api/show endpoint to determine whether a model supports tools.
/// Results are cached per base URL + model ID to avoid repeated network calls.
/// </summary>
public static class OllamaCapabilityChecker
{
    private static readonly Dictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Returns true if the Ollama model supports tool calling.
    /// Falls back to false on any error (safe default — strip tools rather than crash).
    /// </summary>
    public static async Task<bool> SupportsToolsAsync(
        HttpClient httpClient,
        string ollamaBaseUrl,
        string modelId,
        CancellationToken cancellationToken = default)
    {
        // Derive the Ollama API base from the /v1 chat completions base URL
        // e.g. http://localhost:11434/v1 -> http://localhost:11434
        var apiBase = ollamaBaseUrl
            .TrimEnd('/')
            .Replace("/v1", "", StringComparison.OrdinalIgnoreCase);

        var cacheKey = $"{apiBase}|{modelId}";

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                var response = await httpClient.PostAsJsonAsync(
                    $"{apiBase}/api/show",
                    new { name = modelId },
                    cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _cache[cacheKey] = false;
                    return false;
                }

                var body = await response.Content.ReadFromJsonAsync<OllamaShowResponse>(
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var supportsTools = body?.Capabilities?.Contains("tools", StringComparer.OrdinalIgnoreCase) == true;
                _cache[cacheKey] = supportsTools;
                return supportsTools;
            }
            catch
            {
                // Network error, timeout, parse failure — assume no tools (safe default)
                _cache[cacheKey] = false;
                return false;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed class OllamaShowResponse
    {
        public List<string>? Capabilities { get; set; }
    }
}
