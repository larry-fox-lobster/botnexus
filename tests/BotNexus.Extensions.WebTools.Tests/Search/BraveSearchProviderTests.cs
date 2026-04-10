using System.Net;
using BotNexus.Extensions.WebTools.Search;
using BotNexus.Extensions.WebTools.Tests.Helpers;
using FluentAssertions;

namespace BotNexus.Extensions.WebTools.Tests.Search;

[Trait("Category", "Unit")]
public class BraveSearchProviderTests
{
    [Fact]
    public async Task SearchAsync_WithValidResponse_MapsResults()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "web": {
                "results": [
                  { "title": "A", "url": "https://example.com/a", "description": "Alpha" },
                  { "title": "B", "url": "https://example.com/b", "description": "Beta" }
                ]
              }
            }
            """);
        var provider = new BraveSearchProvider(new HttpClient(handler), "api-key");

        var results = await provider.SearchAsync("query", 5, CancellationToken.None);

        results.Should().HaveCount(2);
        results[0].Title.Should().Be("A");
        results[0].Url.Should().Be("https://example.com/a");
        results[0].Snippet.Should().Be("Alpha");
    }

    [Fact]
    public async Task SearchAsync_WithHttp401_ThrowsAuthError()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{"error":"invalid key"}""");
        var provider = new BraveSearchProvider(new HttpClient(handler), "bad-key");

        var act = () => provider.SearchAsync("query", 5, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*401*");
    }

    [Fact]
    public async Task SearchAsync_WithHttp429_ThrowsRateLimitError()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse((HttpStatusCode)429, """{"error":"rate limit"}""");
        var provider = new BraveSearchProvider(new HttpClient(handler), "api-key");

        var act = () => provider.SearchAsync("query", 5, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*429*");
    }

    [Fact]
    public async Task SearchAsync_WithMalformedJson_ThrowsJsonException()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "{not-json");
        var provider = new BraveSearchProvider(new HttpClient(handler), "api-key");

        var act = () => provider.SearchAsync("query", 5, CancellationToken.None);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task SearchAsync_WithMissingApiKey_StillExecutesRequest(string? apiKey)
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"web":{"results":[]}}""");
        var provider = new BraveSearchProvider(new HttpClient(handler), apiKey!);

        var results = await provider.SearchAsync("query", 5, CancellationToken.None);
        results.Should().BeEmpty();
        handler.Requests.Should().ContainSingle();
    }
}
