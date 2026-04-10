using System.Net;
using BotNexus.Extensions.WebTools.Search;
using BotNexus.Extensions.WebTools.Tests.Helpers;
using FluentAssertions;

namespace BotNexus.Extensions.WebTools.Tests.Search;

[Trait("Category", "Unit")]
public class TavilySearchProviderTests
{
    [Fact]
    public async Task SearchAsync_WithValidResponse_MapsResults()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "results": [
                { "title": "A", "url": "https://example.com/a", "content": "Alpha" },
                { "title": "B", "url": "https://example.com/b", "content": "Beta" }
              ]
            }
            """);
        var provider = new TavilySearchProvider(new HttpClient(handler), "api-key");

        var results = await provider.SearchAsync("query", 5, CancellationToken.None);

        results.Should().HaveCount(2);
        results[1].Title.Should().Be("B");
        results[1].Snippet.Should().Be("Beta");
    }

    [Fact]
    public async Task SearchAsync_WithHttp401_ThrowsAuthError()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{"error":"unauthorized"}""");
        var provider = new TavilySearchProvider(new HttpClient(handler), "bad-key");

        var act = () => provider.SearchAsync("query", 5, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*401*");
    }

    [Fact]
    public async Task SearchAsync_WithHttp429_ThrowsRateLimitError()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse((HttpStatusCode)429, """{"error":"rate limit"}""");
        var provider = new TavilySearchProvider(new HttpClient(handler), "api-key");

        var act = () => provider.SearchAsync("query", 5, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*429*");
    }

    [Fact]
    public async Task SearchAsync_WithMalformedJson_ThrowsJsonException()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "{broken");
        var provider = new TavilySearchProvider(new HttpClient(handler), "api-key");

        var act = () => provider.SearchAsync("query", 5, CancellationToken.None);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task SearchAsync_WithMissingApiKey_StillPostsRequest(string? apiKey)
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"results":[]}""");
        var provider = new TavilySearchProvider(new HttpClient(handler), apiKey!);

        var results = await provider.SearchAsync("query", 5, CancellationToken.None);

        results.Should().BeEmpty();
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Body.Should().Contain("\"api_key\"");
    }
}
