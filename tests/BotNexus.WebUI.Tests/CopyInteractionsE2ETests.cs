using FluentAssertions;
using Microsoft.Playwright;

namespace BotNexus.WebUI.Tests;

[Trait("Category", "E2E")]
public sealed class CopyInteractionsE2ETests : IAsyncLifetime
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
    public async Task CopySessionId_CopiesToClipboard()
    {
        var host = await OpenChatAsync();
        var sessionId = await host.SendMessageAsync("copy-session");
        await host.WaitForStreamingCompleteAsync();
        await StubClipboardAsync(host);

        await host.Page.ClickAsync("#btn-copy-session-id");
        var copied = await host.Page.EvaluateAsync<string>("() => window.__copiedText || ''");
        copied.Should().Be(sessionId);
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task CopyMessage_CopiesRawContent()
    {
        var host = await OpenChatAsync();
        host.Supervisor.EnqueueAgentStreamPlan(AgentA, new RecordingStreamPlan
        {
            ContentDeltas = { "**bold** text" }
        });

        await host.SendMessageAsync("copy-message");
        await host.WaitForStreamingCompleteAsync();
        await StubClipboardAsync(host);

        await host.Page.ClickAsync("#chat-messages .message.assistant .btn-copy-msg");
        var copied = await host.Page.EvaluateAsync<string>("() => window.__copiedText || ''");
        copied.Should().Be("**bold** text");
    }

    private static Task StubClipboardAsync(WebUiE2ETestHost host)
        => host.Page.EvaluateAsync(
            @"() => {
                window.__copiedText = '';
                if (!navigator.clipboard) {
                    Object.defineProperty(navigator, 'clipboard', { value: {}, configurable: true });
                }
                navigator.clipboard.writeText = (text) => { window.__copiedText = text; return Promise.resolve(); };
            }");

    private async Task<WebUiE2ETestHost> OpenChatAsync()
    {
        var host = GetHost();
        await host.OpenAgentTimelineAsync(AgentA);
        return host;
    }

    private WebUiE2ETestHost GetHost()
        => _host ?? throw new InvalidOperationException("Playwright host was not initialized.");
}
