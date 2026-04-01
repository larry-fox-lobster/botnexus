using BotNexus.Core.Abstractions;
using BotNexus.Core.Models;

namespace BotNexus.Agent.Tools;

/// <summary>Tool for basic HTTP web requests.</summary>
public sealed class WebTool : ITool
{
    private readonly HttpClient _httpClient;

    public WebTool(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "BotNexus/1.0");
    }

    /// <inheritdoc/>
    public ToolDefinition Definition => new(
        "web_fetch",
        "Fetch the content of a URL via HTTP GET.",
        new Dictionary<string, ToolParameterSchema>
        {
            ["url"] = new("string", "The URL to fetch", Required: true)
        });

    /// <inheritdoc/>
    public async Task<string> ExecuteAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var url = arguments.GetValueOrDefault("url")?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
            return "Error: url is required";

        try
        {
            var response = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            return response.Length > 10000 ? response[..10000] + "\n... (truncated)" : response;
        }
        catch (Exception ex)
        {
            return $"Error fetching {url}: {ex.Message}";
        }
    }
}
