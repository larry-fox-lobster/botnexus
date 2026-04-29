using System.Text.Json;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;
using Microsoft.JSInterop;
using NSubstitute;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests;

/// <summary>
/// Unit tests for <see cref="ConversationHistoryCache"/>.
/// </summary>
public sealed class ConversationHistoryCacheTests
{
    private static List<ChatMessage> SampleMessages() =>
    [
        new ChatMessage("User", "Hello", DateTimeOffset.UtcNow),
        new ChatMessage("Assistant", "Hi there!", DateTimeOffset.UtcNow)
    ];

    // ── GetAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_returns_null_when_localStorage_is_empty()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<string?>("localStorage.getItem", Arg.Any<object[]>())
            .Returns(new ValueTask<string?>((string?)null));

        var cache = new ConversationHistoryCache(js);
        var result = await cache.GetAsync("conv-1");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_returns_null_when_entry_is_older_than_5_minutes()
    {
        var js = Substitute.For<IJSRuntime>();

        var stale = new CachedHistory("conv-1", DateTimeOffset.UtcNow.AddMinutes(-6), SampleMessages());
        var json = JsonSerializer.Serialize(stale);

        js.InvokeAsync<string?>("localStorage.getItem", Arg.Any<object[]>())
            .Returns(new ValueTask<string?>(json));

        var cache = new ConversationHistoryCache(js);
        var result = await cache.GetAsync("conv-1");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_returns_messages_when_entry_is_fresh()
    {
        var js = Substitute.For<IJSRuntime>();

        var messages = SampleMessages();
        var entry = new CachedHistory("conv-1", DateTimeOffset.UtcNow.AddMinutes(-1), messages);
        var json = JsonSerializer.Serialize(entry);

        js.InvokeAsync<string?>("localStorage.getItem", Arg.Any<object[]>())
            .Returns(new ValueTask<string?>(json));

        var cache = new ConversationHistoryCache(js);
        var result = await cache.GetAsync("conv-1");

        result.ShouldNotBeNull();
        result.Messages.Count.ShouldBe(2);
        result.Messages[0].Role.ShouldBe("User");
        result.Messages[1].Role.ShouldBe("Assistant");
    }

    // ── SetAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAsync_writes_json_to_localStorage()
    {
        var js = Substitute.For<IJSRuntime>();

        var cache = new ConversationHistoryCache(js);
        await cache.SetAsync("conv-2", SampleMessages());

        // InvokeVoidAsync is an extension that calls InvokeAsync<IJSVoidResult>
        await js.Received(1).InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "localStorage.setItem",
            Arg.Is<object[]>(a => (string)a[0] == "bn:conv-history:conv-2"));
    }

    // ── InvalidateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task InvalidateAsync_removes_key_from_localStorage()
    {
        var js = Substitute.For<IJSRuntime>();

        var cache = new ConversationHistoryCache(js);
        await cache.InvalidateAsync("conv-3");

        await js.Received(1).InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "localStorage.removeItem",
            Arg.Is<object[]>(a => (string)a[0] == "bn:conv-history:conv-3"));
    }
}
