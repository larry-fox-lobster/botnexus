using Microsoft.Playwright;

namespace BotNexus.WebUI.Tests;

[Trait("Category", "E2E")]
[Collection("Playwright")]
public sealed class HistoryPaginationE2ETests
{
    private const string AgentA = "agent-a";
    private readonly PlaywrightFixture _fixture;

    public HistoryPaginationE2ETests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }
[PlaywrightFact(Timeout = 90000)]
    public async Task TimelineLoad_ShowsSessionDividers()
    {
        await using var host = await _fixture.CreatePageAsync();
        _fixture.SeedScrollbackHistory(AgentA, sessionCount: 3, entriesPerSession: 20);
        await host.OpenAgentTimelineAsync(AgentA);

        var dividerCount = await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .session-divider").CountAsync();
        dividerCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task LoadOlderSessions_ExpandsHistory()
    {
        await using var host = await _fixture.CreatePageAsync();
        _fixture.SeedScrollbackHistory(AgentA, sessionCount: 4, entriesPerSession: 40);
        await host.OpenAgentTimelineAsync(AgentA);

        await Assertions.Expect(host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .history-sentinel")).ToHaveCountAsync(1);
        var initialMessages = await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .message").CountAsync();
        var initialDividers = await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .session-divider").CountAsync();

        await host.Page.EvaluateAsync("() => document.querySelector('.channel-view.active .channel-messages').scrollTop = 0");
        await host.Page.WaitForFunctionAsync(
            $"() => document.querySelectorAll('.channel-view.active .channel-messages .message').length > {initialMessages}");

        var finalMessages = await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .message").CountAsync();
        var finalDividers = await host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .session-divider").CountAsync();
        finalMessages.ShouldBeGreaterThan(initialMessages);
        finalDividers.ShouldBeGreaterThanOrEqualTo(initialDividers);
    }
}





