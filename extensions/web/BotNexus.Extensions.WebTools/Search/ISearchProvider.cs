namespace BotNexus.Extensions.WebTools.Search;

/// <summary>
/// Interface for web search providers.
/// </summary>
internal interface ISearchProvider
{
    /// <summary>
    /// Performs a web search and returns up to maxResults results.
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct);
}

/// <summary>
/// Represents a single search result.
/// </summary>
internal sealed record SearchResult(string Title, string Url, string Snippet);
