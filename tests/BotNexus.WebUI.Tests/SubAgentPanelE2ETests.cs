using BotNexus.Gateway.Abstractions.Models;
using Microsoft.Playwright;
using System.Net.Http.Json;

namespace BotNexus.WebUI.Tests;

[Trait("Category", "E2E")]
[Collection("Playwright")]
public sealed class SubAgentPanelE2ETests
{
    private const string AgentA = "agent-a";
    private readonly PlaywrightFixture _fixture;

    public SubAgentPanelE2ETests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }
[PlaywrightFact(Timeout = 90000)]
    public async Task SubAgentSpawned_ShowsPanel()
    {
        var host = await OpenChatAsync();
        await host.SendMessageAsync("subagent-panel");
        await host.WaitForStreamingCompleteAsync();
        var sessionId = await host.WaitForCurrentSessionIdAsync();
        await SeedSubAgentsForAllAgentSessionsAsync(host, CreateSubAgent(sessionId, "sa-1", SubAgentStatus.Running));
        await host.OpenAgentTimelineAsync(AgentA);
        await RefreshSubAgentsAsync(host);

        await Assertions.Expect(host.Page.Locator("#subagent-panel")).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(host.Page.Locator("#subagent-list .subagent-item")).ToContainTextAsync("worker-sa-1", new() { Timeout = 15000 });
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task SubAgentCompleted_UpdatesStatus()
    {
        var host = await OpenChatAsync();
        await host.SendMessageAsync("subagent-complete");
        await host.WaitForStreamingCompleteAsync();
        var sessionId = await host.WaitForCurrentSessionIdAsync();

        await SeedSubAgentsForAllAgentSessionsAsync(host, CreateSubAgent(sessionId, "sa-done", SubAgentStatus.Completed));
        await host.OpenAgentTimelineAsync(AgentA);
        await RefreshSubAgentsAsync(host);

        await Assertions.Expect(host.Page.Locator("#subagent-list .subagent-item .subagent-status-icon")).ToContainTextAsync("✅", new() { Timeout = 15000 });
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task SubAgentFailed_UpdatesStatus()
    {
        var host = await OpenChatAsync();
        await host.SendMessageAsync("subagent-failed");
        await host.WaitForStreamingCompleteAsync();
        var sessionId = await host.WaitForCurrentSessionIdAsync();

        await SeedSubAgentsForAllAgentSessionsAsync(host, CreateSubAgent(sessionId, "sa-fail", SubAgentStatus.Failed));
        await host.OpenAgentTimelineAsync(AgentA);
        await RefreshSubAgentsAsync(host);

        await Assertions.Expect(host.Page.Locator("#subagent-list .subagent-item .subagent-status-icon")).ToContainTextAsync("❌", new() { Timeout = 15000 });
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task KillButton_IsRenderedForRunningSubAgent()
    {
        var host = await OpenChatAsync();
        await host.SendMessageAsync("subagent-kill");
        await host.WaitForStreamingCompleteAsync();
        var sessionId = await host.WaitForCurrentSessionIdAsync();

        await SeedSubAgentsForAllAgentSessionsAsync(host, CreateSubAgent(sessionId, "sa-kill", SubAgentStatus.Running));
        await host.OpenAgentTimelineAsync(AgentA);
        await RefreshSubAgentsAsync(host);
        await Assertions.Expect(host.Page.Locator("#subagent-list .btn-kill-subagent")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [PlaywrightFact(Timeout = 90000)]
    public async Task NoSubAgents_PanelHidden()
    {
        var host = await OpenChatAsync();
        await host.SendMessageAsync("subagent-none");
        await host.WaitForStreamingCompleteAsync();

        await host.OpenAgentTimelineAsync(AgentA);
        await Assertions.Expect(host.Page.Locator("#subagent-panel")).ToBeHiddenAsync();
    }

    private static SubAgentInfo CreateSubAgent(string sessionId, string subAgentId, SubAgentStatus status)
        => new()
        {
            SubAgentId = subAgentId,
            ParentSessionId = BotNexus.Domain.Primitives.SessionId.From(sessionId),
            ChildSessionId = BotNexus.Domain.Primitives.SessionId.From($"child-{subAgentId}"),
            Name = $"worker-{subAgentId}",
            Task = "Investigate issue",
            Model = "gpt-4.1",
            Status = status,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-10),
            CompletedAt = status == SubAgentStatus.Running ? null : DateTimeOffset.UtcNow,
            TurnsUsed = status == SubAgentStatus.Running ? 0 : 2,
            ResultSummary = status == SubAgentStatus.Running ? null : "Finished task."
        };

    private async Task<WebUiE2ETestHost> OpenChatAsync()
    {
        var host = await _fixture.CreatePageAsync();
        await host.OpenAgentTimelineAsync(AgentA);
        return host;
    }

    private static async Task SeedSubAgentsForAllAgentSessionsAsync(WebUiE2ETestHost host, SubAgentInfo prototype)
    {
        var sessions = await host.ApiClient.GetFromJsonAsync<List<GatewaySession>>($"/api/sessions?agentId={AgentA}") ?? [];
        foreach (var session in sessions.Where(s => string.Equals(s.ChannelType, "web chat", StringComparison.OrdinalIgnoreCase)))
        {
            var copy = new SubAgentInfo
            {
                SubAgentId = prototype.SubAgentId,
                ParentSessionId = session.SessionId,
                ChildSessionId = prototype.ChildSessionId,
                Name = prototype.Name,
                Task = prototype.Task,
                Model = prototype.Model,
                Status = prototype.Status,
                StartedAt = prototype.StartedAt,
                CompletedAt = prototype.CompletedAt,
                TurnsUsed = prototype.TurnsUsed,
                ResultSummary = prototype.ResultSummary
            };
            host.SubAgentManager.SetSubAgents(session.SessionId, copy);
        }
    }

    private static Task RefreshSubAgentsAsync(WebUiE2ETestHost host)
        => host.Page.EvaluateAsync(
            @"() => {
                const button = document.querySelector('#btn-refresh-subagents');
                if (button) button.click();
            }");
}





