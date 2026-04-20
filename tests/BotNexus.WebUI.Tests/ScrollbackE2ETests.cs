using Microsoft.Playwright;

namespace BotNexus.WebUI.Tests;

[Trait("Category", "E2E")]
[Collection("Playwright")]
public sealed class ScrollbackE2ETests
{
    private const string AgentA = "agent-a";
    private const string AgentB = "agent-b";
    private readonly PlaywrightFixture _fixture;

    public ScrollbackE2ETests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task InitialLoad_ShowsNewestMessages()
    {
        await using var host = await _fixture.CreatePageAsync();
        _fixture.SeedScrollbackHistory(AgentA, sessionCount: 2, entriesPerSession: 40);

        await host.OpenAgentTimelineAsync(AgentA);
        await Assertions.Expect(host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .history-sentinel")).ToHaveCountAsync(1);

        var atBottom = await host.Page.EvaluateAsync<bool>(
            "() => { const el = document.querySelector('.channel-view.active .channel-messages'); return el && el.scrollHeight > el.clientHeight && Math.abs((el.scrollHeight - el.clientHeight) - el.scrollTop) < 4; }");
        atBottom.ShouldBeTrue();

        var lastMessage = await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .message .msg-content").Last.InnerTextAsync();
        lastMessage.ShouldContain("agent-a-s1-m39");
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task ScrollUp_LoadsOlderMessages()
    {
        await using var host = await _fixture.CreatePageAsync();
        _fixture.SeedScrollbackHistory(AgentA, sessionCount: 2, entriesPerSession: 40);

        await host.OpenAgentTimelineAsync(AgentA);
        var initialCount = await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .message").CountAsync();

        await host.Page.EvaluateAsync("() => document.querySelector('.channel-view.active .channel-messages').scrollTop = 0");
        await host.Page.WaitForFunctionAsync(
            $"() => document.querySelectorAll('.channel-view.active .channel-messages .message').length > {initialCount}");

        var finalCount = await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .message").CountAsync();
        finalCount.ShouldBeGreaterThan(initialCount);
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task SessionDivider_RenderedAtBoundary()
    {
        await using var host = await _fixture.CreatePageAsync();
        _fixture.SeedScrollbackHistory(AgentA, sessionCount: 2, entriesPerSession: 40);

        await host.OpenAgentTimelineAsync(AgentA);
        var dividerCount = await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .session-divider").CountAsync();

        dividerCount.ShouldBeGreaterThan(0);
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task EndOfHistory_ShowsMarker()
    {
        await using var host = await _fixture.CreatePageAsync();
        _fixture.SeedScrollbackHistory(AgentA, sessionCount: 2, entriesPerSession: 40);

        await host.OpenAgentTimelineAsync(AgentA);
        await host.Page.EvaluateAsync("() => document.querySelector('.channel-view.active .channel-messages').scrollTop = 0");

        var marker = host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .end-of-history");
        await Assertions.Expect(marker).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(marker).ToContainTextAsync("Beginning of conversation history");
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task NoScrollJump_OnPrepend()
    {
        await using var host = await _fixture.CreatePageAsync();
        _fixture.SeedScrollbackHistory(AgentA, sessionCount: 3, entriesPerSession: 40);

        await host.OpenAgentTimelineAsync(AgentA);

        await host.Page.EvaluateAsync("() => document.querySelector('.channel-view.active .channel-messages').scrollTop = 0");
        var anchor = await host.Page.EvaluateAsync<ScrollAnchor>(
            @"() => {
                const chat = document.querySelector('.channel-view.active .channel-messages');
                const first = chat.querySelector('.message .msg-content');
                return { text: first?.innerText ?? '', top: first?.getBoundingClientRect().top ?? 0, count: chat.querySelectorAll('.message').length };
            }");

        await host.Page.WaitForFunctionAsync(
            $"() => document.querySelectorAll('.channel-view.active .channel-messages .message').length > {anchor.Count}");

        var topAfter = await host.Page.EvaluateAsync<double>(
            @"anchorText => {
                const items = Array.from(document.querySelectorAll('.channel-view.active .channel-messages .message .msg-content'));
                const target = items.find(el => el.innerText === anchorText);
                return target ? target.getBoundingClientRect().top : Number.NaN;
            }",
            anchor.Text);

        topAfter.ShouldNotBe(double.NaN);
        Math.Abs(topAfter - anchor.Top).ShouldBeLessThan(4);
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task ChannelSwitch_ResetsScrollback()
    {
        await using var host = await _fixture.CreatePageAsync();
        _fixture.SeedScrollbackHistory(AgentA, sessionCount: 2, entriesPerSession: 40);
        _fixture.SeedScrollbackHistory(AgentB, sessionCount: 1, entriesPerSession: 12);

        await host.OpenAgentTimelineAsync(AgentA);
        var beforeLoadCount = await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .message").CountAsync();
        await host.Page.EvaluateAsync("() => document.querySelector('.channel-view.active .channel-messages').scrollTop = 0");
        await host.Page.WaitForFunctionAsync(
            $"() => document.querySelectorAll('.channel-view.active .channel-messages .message').length > {beforeLoadCount}");

        var loadedFromAgentA = await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .message .msg-content").First.InnerTextAsync();

        await host.OpenAgentTimelineAsync(AgentB);
        await Assertions.Expect(host.Page.Locator("#chat-title")).ToContainTextAsync(AgentB, new() { Timeout = 15000 });
        await Assertions.Expect(host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .history-sentinel")).ToHaveCountAsync(1);
        await Assertions.Expect(host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .end-of-history")).ToBeVisibleAsync();

        (await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat}").InnerTextAsync()).ShouldNotContain(loadedFromAgentA);
    }

    private sealed class ScrollAnchor
    {
        public string Text { get; set; } = string.Empty;
        public double Top { get; set; }
        public int Count { get; set; }
    }
}
