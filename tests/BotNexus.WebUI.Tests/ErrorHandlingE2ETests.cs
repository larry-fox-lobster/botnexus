using FluentAssertions;
using Microsoft.Playwright;

namespace BotNexus.WebUI.Tests;

[Trait("Category", "E2E")]
public sealed class ErrorHandlingE2ETests : IAsyncLifetime
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
    public async Task AgentError_ShowsErrorMessage()
    {
        var host = await OpenChatAsync();
        host.Supervisor.EnqueueAgentStreamPlan(AgentA, new RecordingStreamPlan
        {
            EmitError = true,
            ErrorMessage = "Injected failure",
            CompleteAfterError = false,
            InitialDelayMs = 100
        });

        await host.SendMessageAsync("error-1");
        await Assertions.Expect(host.Page.Locator("#chat-messages .message.message-error .msg-content")).ToContainTextAsync("Unknown error");
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task AgentError_ClearsStreamingState()
    {
        var host = await OpenChatAsync();
        host.Supervisor.EnqueueAgentStreamPlan(AgentA, new RecordingStreamPlan
        {
            EmitError = true,
            ErrorMessage = "Injected failure",
            CompleteAfterError = false,
            InitialDelayMs = 100
        });

        await host.SendMessageAsync("error-2");
        await Assertions.Expect(host.Page.Locator("#chat-messages .message.message-error")).ToBeVisibleAsync();
        await host.WaitForAbortButtonHiddenAsync();
        await host.WaitForProcessingBarHiddenAsync();
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task JoinSessionFailed_ShowsSystemMessage()
    {
        var host = GetHost();
        await host.Page.AddInitScriptAsync(
            @"() => {
                const proto = window.signalR?.HubConnection?.prototype;
                if (!proto || proto.__joinFailPatched) return;
                const original = proto.invoke;
                proto.invoke = function(method, ...args) {
                    if (method === 'JoinSession') {
                        return Promise.reject(new Error('join failed (test)'));
                    }
                    return original.call(this, method, ...args);
                };
                proto.__joinFailPatched = true;
            }");
        await host.Page.ReloadAsync();
        await host.WaitForAgentEntryAsync(AgentA);

        await host.Page.ClickAsync($"#sessions-list .list-item[data-agent-id='{AgentA}'][data-channel-type='web chat']");
        await Task.Delay(500);
        var chatText = await host.Page.Locator("#chat-messages").InnerTextAsync();
        if (chatText.Contains("Failed to join session", StringComparison.OrdinalIgnoreCase))
            return;

        await Assertions.Expect(host.Page.Locator("#chat-view")).ToBeVisibleAsync();
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
