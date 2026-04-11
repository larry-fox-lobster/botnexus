using Microsoft.Playwright;

namespace BotNexus.WebUI.Tests;

[Trait("Category", "E2E")]
public sealed class ModalDialogE2ETests : IAsyncLifetime
{
    private WebUiE2ETestHost? _host;

    public async Task InitializeAsync() => _host = await WebUiE2ETestHost.StartAsync();

    public async Task DisposeAsync()
    {
        if (_host is not null)
            await _host.DisposeAsync();
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task ConfirmDialog_OkExecutesCallback()
    {
        var host = GetHost();
        await host.Page.ClickAsync("#btn-stop-gateway");
        await Assertions.Expect(host.Page.Locator("#confirm-dialog")).ToBeVisibleAsync();
        await host.Page.ClickAsync("#btn-confirm-ok");
        await Assertions.Expect(host.Page.Locator("#chat-messages .message.system-msg")).ToContainTextAsync("Gateway restart initiated.");
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task ConfirmDialog_CancelDismisses()
    {
        var host = GetHost();
        await host.Page.ClickAsync("#btn-stop-gateway");
        await Assertions.Expect(host.Page.Locator("#confirm-dialog")).ToBeVisibleAsync();
        await host.Page.ClickAsync("#btn-confirm-cancel");
        await Assertions.Expect(host.Page.Locator("#confirm-dialog")).ToBeHiddenAsync();
    }

    private WebUiE2ETestHost GetHost()
        => _host ?? throw new InvalidOperationException("Playwright host was not initialized.");
}
