using FluentAssertions;
using Microsoft.Playwright;

namespace BotNexus.WebUI.Tests;

[Trait("Category", "E2E")]
public sealed class HistoryPaginationE2ETests : IAsyncLifetime
{
    private const string AgentA = "agent-a";
    private WebUiE2ETestHost? _host;

    public async Task InitializeAsync() => _host = await WebUiE2ETestHost.StartAsync();

    public async Task DisposeAsync()
    {
        if (_host is not null)
            await _host.DisposeAsync();
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task TimelineLoad_ShowsSessionDividers()
    {
        var host = GetHost();
        await SeedSessionsAsync(host, 2);
        await host.OpenAgentTimelineAsync(AgentA);

        var dividerCount = await host.Page.Locator("#chat-messages .session-divider").CountAsync();
        dividerCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task LoadOlderSessions_ExpandsHistory()
    {
        var host = GetHost();
        await SeedSessionsAsync(host, 4);
        await host.OpenAgentTimelineAsync(AgentA);

        var loadOlder = host.Page.Locator("#chat-messages .load-more-history").First;
        await Assertions.Expect(loadOlder).ToContainTextAsync("older session");
        var before = await host.Page.Locator("#chat-messages .session-divider").CountAsync();

        await loadOlder.ClickAsync();
        await Assertions.Expect(host.Page.Locator("#chat-messages .load-more-history")).ToHaveCountAsync(0, new() { Timeout = 15000 });
        var after = await host.Page.Locator("#chat-messages .session-divider").CountAsync();
        after.Should().BeGreaterThan(before);
    }

    private static async Task SeedSessionsAsync(WebUiE2ETestHost host, int sessionCount)
    {
        await host.OpenAgentTimelineAsync(AgentA);
        for (var i = 0; i < sessionCount; i++)
        {
            await host.SendMessageAsync($"seed-session-{i}");
            await host.WaitForStreamingCompleteAsync();

            if (i < sessionCount - 1)
            {
                await host.Page.FillAsync("#chat-input", "/new");
                await host.Page.PressAsync("#chat-input", "Enter");
                await Assertions.Expect(host.Page.Locator("#chat-messages")).ToContainTextAsync("New session started");
            }
        }
    }

    private WebUiE2ETestHost GetHost()
        => _host ?? throw new InvalidOperationException("Playwright host was not initialized.");
}
