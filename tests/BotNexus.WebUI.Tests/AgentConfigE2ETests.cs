using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;

namespace BotNexus.WebUI.Tests;

[Trait("Category", "E2E")]
public sealed class AgentConfigE2ETests : IAsyncLifetime
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
    public async Task ClickAgent_OpensConfigView()
    {
        var host = GetHost();
        await host.Page.ClickAsync($"#agents-list .list-item:has-text('{AgentA}')");
        await Assertions.Expect(host.Page.Locator("#agent-config-view")).ToBeVisibleAsync();
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task SaveConfig_PutsUpdatedAgent()
    {
        var host = GetHost();
        await host.Page.ClickAsync($"#agents-list .list-item:has-text('{AgentA}')");

        const string displayName = "Agent A Updated";
        await host.Page.FillAsync("#cfg-displayName", displayName);
        await host.Page.ClickAsync("#btn-agent-save");
        await Assertions.Expect(host.Page.Locator("#chat-messages .message.system-msg")).ToContainTextAsync("Agent settings saved.");

        var agent = await host.ApiClient.GetFromJsonAsync<JsonElement>($"/api/agents/{AgentA}");
        agent.GetProperty("displayName").GetString().Should().Be(displayName);
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task OpenChat_SwitchesToChatView()
    {
        var host = GetHost();
        await host.Page.ClickAsync($"#agents-list .list-item:has-text('{AgentA}')");
        await host.Page.ClickAsync("#btn-agent-chat");
        await Assertions.Expect(host.Page.Locator("#chat-view")).ToBeVisibleAsync();
        await Assertions.Expect(host.Page.Locator("#chat-title")).ToContainTextAsync(AgentA);
    }

    private WebUiE2ETestHost GetHost()
        => _host ?? throw new InvalidOperationException("Playwright host was not initialized.");
}
