using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Layout;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests;

/// <summary>
/// bUnit tests for <see cref="MainLayout"/>.
/// MainLayout is the app shell: full-width banner, announcements bar, and
/// a two-column body (sidebar + main canvas). The sidebar owns the agent
/// dropdown, session list, connection status, nav links, and restart button.
/// Agent-level concerns (streaming indicators, unread counts) are surfaced
/// through the dropdown option text, not standalone elements.
/// </summary>
public sealed class MainLayoutTests : IDisposable
{
    private readonly BunitContext _ctx = new();
    private readonly AgentSessionManager _manager;

    public MainLayoutTests()
    {
        _manager = TestSessionFactory.CreateManager();

        _ctx.Services.AddSingleton(_manager);
        _ctx.Services.AddSingleton(_manager.Hub);
        _ctx.Services.AddSingleton(new HttpClient());
        _ctx.Services.AddScoped(sp => new GatewayInfoService(sp.GetRequiredService<HttpClient>(), _manager));
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _manager.Dispose();
        _ctx.Dispose();
    }

    // ── Banner ───────────────────────────────────────────────────────────

    [Fact]
    public void Renders_banner_with_BotNexus_branding()
    {
        var cut = RenderLayout();

        cut.Find(".app-banner").ShouldNotBeNull();
        cut.Find(".banner-title").TextContent.ShouldContain("BotNexus");
    }

    [Fact]
    public void Renders_banner_logo_emoji()
    {
        var cut = RenderLayout();

        cut.Find(".banner-logo").ShouldNotBeNull();
    }

    // ── Layout structure ─────────────────────────────────────────────────

    [Fact]
    public void Renders_two_column_layout()
    {
        var cut = RenderLayout();

        cut.Find(".app-body").ShouldNotBeNull();
        cut.Find(".main-sidebar").ShouldNotBeNull();
        cut.Find(".main-canvas").ShouldNotBeNull();
    }

    [Fact]
    public void Body_content_renders_in_main_canvas()
    {
        var cut = RenderLayout(bodyContent: "<div id='test-body'>hello</div>");

        cut.Find("#test-body").TextContent.ShouldBe("hello");
        // Ensure the body lands inside main-canvas, not sidebar
        cut.Find(".main-canvas #test-body").ShouldNotBeNull();
    }

    // ── Sidebar structure ────────────────────────────────────────────────

    [Fact]
    public void Renders_restart_gateway_button_in_sidebar()
    {
        var cut = RenderLayout();

        var btn = cut.Find(".restart-btn");
        btn.ShouldNotBeNull();
        btn.TextContent.ShouldContain("Restart Gateway");
    }

    [Fact]
    public void Renders_nav_links_in_sidebar()
    {
        var cut = RenderLayout();

        var links = cut.FindAll(".sidebar-nav-item");
        links.ShouldNotBeEmpty();
    }

    // ── Agent dropdown ───────────────────────────────────────────────────

    [Fact]
    public void Does_not_render_agent_dropdown_when_no_sessions()
    {
        // No SimulateConnected — Sessions is empty
        var cut = RenderLayout();

        cut.FindAll(".agent-dropdown-container").ShouldBeEmpty();
    }

    [Fact]
    public void Renders_agent_dropdown_when_sessions_exist()
    {
        SimulateConnected(
            new AgentSummary("nova", "Nova"),
            new AgentSummary("spark", "Spark"));

        var cut = RenderLayout();

        cut.Find(".agent-dropdown-container").ShouldNotBeNull();

        var options = cut.FindAll(".agent-dropdown-select option")
            .Where(o => o.GetAttribute("value") != "")
            .ToList();
        options.Count.ShouldBe(2);
    }

    [Fact]
    public void Agent_dropdown_option_text_includes_display_name()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));

        var cut = RenderLayout();

        var option = cut.FindAll(".agent-dropdown-select option")
            .First(o => o.GetAttribute("value") == "nova");
        option.TextContent.ShouldContain("Nova");
    }

    [Fact]
    public void Agent_dropdown_option_shows_streaming_indicator_when_streaming()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));

        // Set streaming before rendering so the initial render picks it up
        _manager.Sessions["nova"].IsStreaming = true;

        var cut = RenderLayout();

        var option = cut.FindAll(".agent-dropdown-select option")
            .First(o => o.GetAttribute("value") == "nova");
        option.TextContent.ShouldContain("●");
    }

    [Fact]
    public void Agent_dropdown_option_shows_unread_count_when_nonzero()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));
        _manager.Sessions["nova"].UnreadCount = 5;

        var cut = RenderLayout();

        var option = cut.FindAll(".agent-dropdown-select option")
            .First(o => o.GetAttribute("value") == "nova");
        option.TextContent.ShouldContain("5");
    }

    // ── Announcements bar ────────────────────────────────────────────────

    [Fact]
    public void Announcements_bar_is_hidden_when_no_announcements()
    {
        var cut = RenderLayout();

        cut.FindAll(".announcement-bar").ShouldBeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Renders <see cref="MainLayout"/> with an optional body fragment.
    /// bUnit requires passing a <see cref="RenderFragment"/> for the <c>Body</c>
    /// parameter on <see cref="LayoutComponentBase"/>.
    /// </summary>
    private IRenderedComponent<MainLayout> RenderLayout(string bodyContent = "")
    {
        return _ctx.Render<MainLayout>(p => p
            .Add(x => x.Body, builder =>
            {
                builder.AddMarkupContent(0, bodyContent);
            }));
    }

    private void SimulateConnected(params AgentSummary[] agents)
    {
        var payload = new ConnectedPayload(
            "test-connection-id",
            agents,
            "1.0.0-test",
            new HubCapabilities(true));

        var backingField = typeof(GatewayHubConnection)
            .GetField("OnConnected",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

        if (backingField?.GetValue(_manager.Hub) is Action<ConnectedPayload> del)
            del(payload);
    }
}
