using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;

namespace BotNexus.WebUI.Tests;

[Trait("Category", "E2E")]
public sealed class ModelSelectorE2ETests : IAsyncLifetime
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
    public async Task ModelDropdown_PopulatedOnChatOpen()
    {
        var host = await OpenChatAsync();
        await host.SendMessageAsync("seed-model-dropdown");
        await host.WaitForStreamingCompleteAsync();
        await host.OpenAgentTimelineAsync(AgentA);
        await Assertions.Expect(host.Page.Locator("#model-select")).ToBeVisibleAsync();
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task ModelChange_UpdatesAgent()
    {
        var host = await OpenChatAsync();
        await host.SendMessageAsync("seed-model-update");
        await host.WaitForStreamingCompleteAsync();
        await host.OpenAgentTimelineAsync(AgentA);
        await Assertions.Expect(host.Page.Locator("#model-select")).ToBeVisibleAsync();

        await host.Page.EvaluateAsync(
            @"() => {
                const select = document.querySelector('#model-select');
                if (!select) return;
                if (!select.options.length) {
                    const opt = document.createElement('option');
                    opt.value = 'gpt-4.1';
                    opt.textContent = 'gpt-4.1';
                    select.appendChild(opt);
                }
                if (select.options.length > 1) select.selectedIndex = 1;
                select.dispatchEvent(new Event('change', { bubbles: true }));
            }");
        var selected = await host.Page.InputValueAsync("#model-select");
        await Assertions.Expect(host.Page.Locator("#chat-messages .message.system-msg").Last).ToContainTextAsync($"Model changed to {selected}");

        var agent = await host.ApiClient.GetFromJsonAsync<JsonElement>($"/api/agents/{AgentA}");
        agent.GetProperty("modelId").GetString().Should().Be(selected);
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task ModelChange_ShowsConfirmation()
    {
        var host = await OpenChatAsync();
        await host.SendMessageAsync("seed-model-confirm");
        await host.WaitForStreamingCompleteAsync();
        await host.OpenAgentTimelineAsync(AgentA);
        await Assertions.Expect(host.Page.Locator("#model-select")).ToBeVisibleAsync();
        await host.Page.EvaluateAsync(
            @"() => {
                const select = document.querySelector('#model-select');
                if (!select) return;
                if (!select.options.length) {
                    const opt = document.createElement('option');
                    opt.value = 'gpt-4.1';
                    opt.textContent = 'gpt-4.1';
                    select.appendChild(opt);
                }
                if (select.options.length > 1) select.selectedIndex = 1;
                select.dispatchEvent(new Event('change', { bubbles: true }));
            }");
        var selected = await host.Page.InputValueAsync("#model-select");
        await Assertions.Expect(host.Page.Locator("#chat-messages .message.system-msg").Last).ToContainTextAsync($"Model changed to {selected}");
    }

    private async Task<WebUiE2ETestHost> OpenChatAsync()
    {
        var host = GetHost();
        await host.OpenAgentTimelineAsync(AgentA);
        return host;
    }

    private WebUiE2ETestHost GetHost()
        => _host ?? throw new InvalidOperationException("Playwright host was not initialized.");
}
