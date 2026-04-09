using System.Text;
using System.Text.Json;

namespace BotNexus.Extensions.WebTools.Search;

/// <summary>
/// Tavily Search API provider.
/// Endpoint: POST https://api.tavily.com/search
/// </summary>
internal sealed class TavilySearchProvider : ISearchProvider
{
    private const string ApiEndpoint = "https://api.tavily.com/search";
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public TavilySearchProvider(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct)
    {
        var requestBody = new
        {
            api_key = _apiKey,
            query,
            max_results = maxResults,
            include_answer = false
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(ApiEndpoint, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(responseJson);

        var results = new List<SearchResult>();

        if (doc.RootElement.TryGetProperty("results", out var resultsArray))
        {
            foreach (var result in resultsArray.EnumerateArray())
            {
                var title = result.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var url = result.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                var snippet = result.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";

                if (!string.IsNullOrWhiteSpace(url))
                    results.Add(new SearchResult(title, url, snippet));
            }
        }

        return results;
    }
}
