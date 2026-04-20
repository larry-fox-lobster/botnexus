using Bunit;
using Microsoft.Extensions.DependencyInjection;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Pages;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests;

/// <summary>
/// bUnit tests for the <see cref="Home"/> page component.
/// Home.razor is now a thin page: it renders an empty-state prompt when no
/// agent is selected, and a set of <c>.chat-panel-wrapper</c> divs (one per
/// session) that are toggled active/hidden based on the active agent.
/// Agent selection, connection status, and the sidebar live in MainLayout —
/// see <see cref="MainLayoutTests"/> for those tests.
/// </summary>
public sealed class HomePageTests : IDisposable
{
    private readonly BunitContext _ctx = new();
    private readonly AgentSessionManager _manager;

    public HomePageTests()
    {
        _manager = TestSessionFactory.CreateManager();

        _ctx.Services.AddSingleton(_manager);
        _ctx.Services.AddSingleton(_manager.Hub);

        // Home page uses JS interop for scrolling
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _manager.Dispose();
        _ctx.Dispose();
    }

    // ── Empty state ──────────────────────────────────────────────────────

    [Fact]
    public void Shows_select_agent_prompt_when_no_agent_selected()
    {
        var cut = _ctx.Render<Home>();

        cut.Find(".empty-state").TextContent.ShouldContain("Select an agent");
    }

    [Fact]
    public async Task Does_not_show_empty_state_when_agent_is_selected()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));
        await _manager.SetActiveAgentAsync("nova");

        var cut = _ctx.Render<Home>();

        // Empty state should not be present once an agent is active
        cut.FindAll(".empty-state").ShouldBeEmpty();
    }

    // ── Chat panel wrappers ──────────────────────────────────────────────

    [Fact]
    public void Renders_chat_panel_wrapper_for_each_session()
    {
        SimulateConnected(
            new AgentSummary("nova", "Nova"),
            new AgentSummary("spark", "Spark"));

        var cut = _ctx.Render<Home>();

        cut.FindAll(".chat-panel-wrapper").Count().ShouldBe(2);
    }

    [Fact]
    public async Task Active_session_panel_has_active_class()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));
        await _manager.SetActiveAgentAsync("nova");

        var cut = _ctx.Render<Home>();

        cut.FindAll(".chat-panel-wrapper.active").Count().ShouldBe(1);
    }

    [Fact]
    public async Task Inactive_session_panels_have_hidden_class()
    {
        SimulateConnected(
            new AgentSummary("nova", "Nova"),
            new AgentSummary("spark", "Spark"));

        // Only "nova" is active — "spark" should be hidden
        await _manager.SetActiveAgentAsync("nova");

        var cut = _ctx.Render<Home>();

        cut.FindAll(".chat-panel-wrapper.hidden").Count().ShouldBe(1);
    }

    [Fact]
    public void No_panels_are_active_when_no_agent_selected()
    {
        SimulateConnected(
            new AgentSummary("nova", "Nova"),
            new AgentSummary("spark", "Spark"));
        // ActiveAgentId is null by default

        var cut = _ctx.Render<Home>();

        cut.FindAll(".chat-panel-wrapper.active").ShouldBeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SimulateConnected(params AgentSummary[] agents)
    {
        var payload = new ConnectedPayload(
            "test-connection-id",
            agents,
            "1.0.0-test",
            new HubCapabilities(true));

        // GatewayHubConnection.OnConnected is a public event whose backing field
        // can be retrieved via reflection to fire it in tests.
        var backingField = typeof(GatewayHubConnection)
            .GetField("OnConnected",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

        if (backingField?.GetValue(_manager.Hub) is Action<ConnectedPayload> del)
            del(payload);
    }
}
