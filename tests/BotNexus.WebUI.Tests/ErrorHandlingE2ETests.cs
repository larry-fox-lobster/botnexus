using FluentAssertions;
using Microsoft.Playwright;

namespace BotNexus.WebUI.Tests;

[Trait("Category", "E2E")]
[Collection("Playwright")]
public sealed class ErrorHandlingE2ETests
{
    private const string AgentA = "agent-a";
    private readonly PlaywrightFixture _fixture;

    public ErrorHandlingE2ETests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
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
        await Assertions.Expect(host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .message.message-error .msg-content")).ToContainTextAsync("Unknown error");
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
        await Assertions.Expect(host.Page.Locator($"{WebUiE2ETestHost.ActiveChat} .message.message-error")).ToBeVisibleAsync();
        await host.WaitForAbortButtonHiddenAsync();
        await host.WaitForProcessingBarHiddenAsync();
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task SubscribeAllFailed_DoesNotBlockOpeningTimeline()
    {
        await using var host = await _fixture.CreatePageAsync();
        await host.Page.AddInitScriptAsync(
            @"() => {
                const proto = window.signalR?.HubConnection?.prototype;
                if (!proto || proto.__subscribeAllFailPatched) return;
                const original = proto.invoke;
                proto.invoke = function(method, ...args) {
                    if (method === 'SubscribeAll') {
                        return Promise.reject(new Error('subscribe all failed (test)'));
                    }
                    return original.call(this, method, ...args);
                };
                proto.__subscribeAllFailPatched = true;
            }");
        await host.Page.ReloadAsync();
        await host.WaitForAgentEntryAsync(AgentA);

        await host.Page.ClickAsync($"#sessions-list .list-item[data-agent-id='{AgentA}'][data-channel-type='web chat']");
        await Assertions.Expect(host.Page.Locator("#chat-view")).ToBeVisibleAsync();
        await Assertions.Expect(host.Page.Locator("#chat-input")).ToBeEditableAsync();
    }

    private async Task<WebUiE2ETestHost> OpenChatAsync()
    {
        var host = await _fixture.CreatePageAsync();
        await host.OpenAgentTimelineAsync(AgentA);
        return host;
    }
}





