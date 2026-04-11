using FluentAssertions;
using Microsoft.Playwright;

namespace BotNexus.WebUI.Tests;

[Trait("Category", "E2E")]
public sealed class AddAgentFormE2ETests : IAsyncLifetime
{
    private WebUiE2ETestHost? _host;

    public async Task InitializeAsync() => _host = await WebUiE2ETestHost.StartAsync();

    public async Task DisposeAsync()
    {
        if (_host is not null)
            await _host.DisposeAsync();
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task AddButton_OpensModal()
    {
        var host = GetHost();
        await host.Page.ClickAsync("#btn-add-agent");
        await Assertions.Expect(host.Page.Locator("#agent-form-modal")).ToBeVisibleAsync();
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task SaveAgent_ValidationError()
    {
        var host = GetHost();
        await host.Page.ClickAsync("#btn-add-agent");
        await host.Page.ClickAsync("#btn-save-agent");
        await Assertions.Expect(host.Page.Locator("#form-feedback")).ToContainTextAsync("required");
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task SaveAgent_Success_ClosesModal()
    {
        var host = GetHost();
        await host.Page.ClickAsync("#btn-add-agent");

        var agentName = $"agent-p1-{Guid.NewGuid():N}".Substring(0, 18);
        await host.Page.FillAsync("#form-agent-name", agentName);
        await SelectFirstProviderAndModelAsync(host.Page);
        await host.Page.ClickAsync("#btn-save-agent");

        await Assertions.Expect(host.Page.Locator("#agent-form-modal")).ToBeHiddenAsync(new() { Timeout = 15000 });
        await Assertions.Expect(host.Page.Locator("#agents-list")).ToContainTextAsync(agentName);
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task ProviderChange_LoadsModels()
    {
        var host = GetHost();
        await host.Page.ClickAsync("#btn-add-agent");

        await SelectFirstProviderAsync(host.Page);
        await WaitForModelOptionsAsync(host.Page, expectedMinimum: 2);
    }

    private static async Task SelectFirstProviderAndModelAsync(IPage page)
    {
        await SelectFirstProviderAsync(page);
        await WaitForModelOptionsAsync(page, expectedMinimum: 2);
        await page.SelectOptionAsync("#form-agent-model", new SelectOptionValue { Index = 1 });
    }

    private static async Task SelectFirstProviderAsync(IPage page)
    {
        await ExpectEventuallyAsync(
            async () => await page.Locator("#form-agent-provider option").CountAsync() > 1,
            "Timed out waiting for provider options.");
        await page.SelectOptionAsync("#form-agent-provider", new SelectOptionValue { Index = 1 });
    }

    private static async Task WaitForModelOptionsAsync(IPage page, int expectedMinimum)
    {
        await ExpectEventuallyAsync(
            async () => await page.Locator("#form-agent-model option").CountAsync() >= expectedMinimum,
            "Timed out waiting for provider model options.");
    }

    private static async Task ExpectEventuallyAsync(Func<Task<bool>> predicate, string timeoutMessage, int timeoutMs = 15000)
    {
        var started = DateTimeOffset.UtcNow;
        while ((DateTimeOffset.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            if (await predicate())
                return;
            await Task.Delay(100);
        }

        throw new TimeoutException(timeoutMessage);
    }

    private WebUiE2ETestHost GetHost()
        => _host ?? throw new InvalidOperationException("Playwright host was not initialized.");
}
