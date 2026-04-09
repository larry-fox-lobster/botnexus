using System.Text.Json;

namespace BotNexus.Extensions.WebTools.Search;

/// <summary>
/// Brave Search API provider.
/// Endpoint: GET https://api.search.brave.com/res/v1/web/search
/// </summary>
internal sealed class BraveSearchProvider : ISearchProvider
{
    private const string ApiEndpoint = "https://api.search.brave.com/res/v1/web/search";
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public BraveSearchProvider(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct)
    {
        var requestUrl = $"{ApiEndpoint}?q={Uri.EscapeDataString(query)}&count={maxResults}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("Accept-Encoding", "gzip");
        request.Headers.Add("X-Subscription-Token", _apiKey);

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var results = new List<SearchResult>();

        if (doc.RootElement.TryGetProperty("web", out var web) &&
            web.TryGetProperty("results", out var resultsArray))
        {
            foreach (var result in resultsArray.EnumerateArray())
            {
                var title = result.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var url = result.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                var description = result.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

                if (!string.IsNullOrWhiteSpace(url))
                    results.Add(new SearchResult(title, url, description));
            }
        }

        return results;
    }
}
