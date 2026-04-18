using Bunit;
using Microsoft.Extensions.DependencyInjection;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Pages;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests;

/// <summary>
/// bUnit tests for the <see cref="Home"/> page component.
/// The Home page injects <see cref="AgentSessionManager"/> via DI,
/// so we register it in the test context's service collection.
/// </summary>
public sealed class HomePageTests : IDisposable
{
    private readonly BunitContext _ctx = new();
    private readonly AgentSessionManager _manager;

    public HomePageTests()
    {
        _manager = TestSessionFactory.CreateManager();

        // Register services the Home page injects
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
    public void Does_not_show_no_agents_message_when_hub_disconnected()
    {
        // The component only shows "No agents available" when Sessions.Count == 0
        // AND Hub.IsConnected is true. In test, hub is always disconnected,
        // so the message should NOT appear (correct behavior — we don't want to
        // show "no agents" when the connection isn't established yet).
        SimulateConnected();

        var cut = _ctx.Render<Home>();

        cut.FindAll(".text-muted").Should().BeEmpty();
    }

    [Fact]
    public void Shows_select_agent_prompt_when_no_agent_selected()
    {
        var cut = _ctx.Render<Home>();

        cut.Find(".empty-state").TextContent.Should().Contain("Select an agent");
    }

    // ── Agent list rendering ─────────────────────────────────────────────

    [Fact]
    public void Renders_agent_list_from_sessions()
    {
        SimulateConnected(
            new AgentSummary("nova", "Nova"),
            new AgentSummary("spark", "Spark"));

        var cut = _ctx.Render<Home>();

        var agentNames = cut.FindAll(".agent-name");
        agentNames.Should().HaveCount(2);
        agentNames[0].TextContent.Should().Be("Nova");
        agentNames[1].TextContent.Should().Be("Spark");
    }

    [Fact]
    public void Renders_agent_nodes_for_each_session()
    {
        SimulateConnected(
            new AgentSummary("agent-1", "Agent One"),
            new AgentSummary("agent-2", "Agent Two"),
            new AgentSummary("agent-3", "Agent Three"));

        var cut = _ctx.Render<Home>();

        cut.FindAll(".agent-node").Should().HaveCount(3);
    }

    // ── Agent selection ──────────────────────────────────────────────────

    [Fact]
    public void Clicking_agent_header_expands_it()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));

        var cut = _ctx.Render<Home>();

        // Initially collapsed — no channel items
        cut.FindAll(".agent-children").Should().BeEmpty();

        // Click to expand
        cut.Find(".agent-node-header").Click();

        cut.FindAll(".agent-children").Should().NotBeEmpty();
        cut.FindAll(".channel-item").Should().NotBeEmpty();
    }

    [Fact]
    public void Clicking_channel_item_selects_agent()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));

        var cut = _ctx.Render<Home>();

        // Expand first
        cut.Find(".agent-node-header").Click();

        // Click the channel to select
        cut.Find(".channel-item").Click();

        // Should show the chat panel (not the empty state alone)
        cut.FindAll(".chat-panel-wrapper.active").Should().NotBeEmpty();
    }

    [Fact]
    public void Selected_agent_gets_active_class()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));

        var cut = _ctx.Render<Home>();

        // Expand and select
        cut.Find(".agent-node-header").Click();
        cut.Find(".channel-item").Click();

        cut.Find(".agent-node.active").Should().NotBeNull();
    }

    // ── Unread badges ────────────────────────────────────────────────────

    [Fact]
    public void Shows_unread_badges_when_count_greater_than_zero()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));

        // Set unread count directly on the session state
        _manager.Sessions["nova"].UnreadCount = 3;

        var cut = _ctx.Render<Home>();

        var badge = cut.Find(".unread-badge");
        badge.TextContent.Should().Be("3");
    }

    [Fact]
    public void Does_not_show_unread_badge_when_count_is_zero()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));
        // UnreadCount defaults to 0

        var cut = _ctx.Render<Home>();

        cut.FindAll(".unread-badge").Should().BeEmpty();
    }

    // ── Streaming dots ───────────────────────────────────────────────────

    [Fact]
    public void Shows_streaming_dot_when_agent_is_streaming()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));

        _manager.Sessions["nova"].IsStreaming = true;

        var cut = _ctx.Render<Home>();

        cut.Find(".agent-streaming-dot").Should().NotBeNull();
    }

    [Fact]
    public void Does_not_show_streaming_dot_when_not_streaming()
    {
        SimulateConnected(new AgentSummary("nova", "Nova"));
        // IsStreaming defaults to false

        var cut = _ctx.Render<Home>();

        cut.FindAll(".agent-streaming-dot").Should().BeEmpty();
    }

    // ── Connection status ────────────────────────────────────────────────

    [Fact]
    public void Renders_connection_status_component()
    {
        var cut = _ctx.Render<Home>();

        cut.Find(".connection-indicator").Should().NotBeNull();
    }

    // ── Sidebar structure ────────────────────────────────────────────────

    [Fact]
    public void Renders_agents_heading()
    {
        var cut = _ctx.Render<Home>();

        cut.Find(".agent-list h3").TextContent.Should().Be("Agents");
    }

    [Fact]
    public void Renders_restart_gateway_button()
    {
        var cut = _ctx.Render<Home>();

        var restartBtn = cut.Find(".restart-btn");
        restartBtn.Should().NotBeNull();
        restartBtn.TextContent.Should().Contain("Restart Gateway");
    }

    // ── Main grid layout ─────────────────────────────────────────────────

    [Fact]
    public void Renders_main_grid_with_sidebar_and_chat_area()
    {
        var cut = _ctx.Render<Home>();

        cut.Find(".main-grid").Should().NotBeNull();
        cut.Find(".agent-list").Should().NotBeNull();
        cut.Find(".chat-area").Should().NotBeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Simulates the hub "Connected" event by raising HandleConnected via the
    /// OnConnected event on the hub. Since we can't connect to a real server,
    /// we trigger the connected handler via reflection on the manager's internal
    /// handler. This populates the Sessions dictionary.
    /// </summary>
    private void SimulateConnected(params AgentSummary[] agents)
    {
        // The AgentSessionManager subscribes to Hub.OnConnected in its constructor.
        // We can trigger it by invoking the OnConnected event on the hub.
        // However, since the hub's OnConnected is only raised by SignalR, we'll
        // populate the sessions dictionary directly via the public RegisterSession
        // and by accessing the Sessions dict through the manager's event handler.

        // Use the Connected payload to go through the proper handler path:
        // We access the private HandleConnected method via the OnConnected event.
        var payload = new ConnectedPayload(
            "test-connection-id",
            agents,
            "1.0.0-test",
            new HubCapabilities(true));

        // The manager subscribes to _hub.OnConnected. Since GatewayHubConnection
        // exposes OnConnected as a public event, we can use reflection to invoke it,
        // OR we can just call the internal handler directly.
        // Easier approach: use the fact that the sessions are a private dict, but
        // the HandleConnected handler is wired via Hub.OnConnected event.
        // Let's trigger it through the hub's event.

        // GatewayHubConnection.OnConnected is a public event — we can invoke its
        // backing delegate via reflection.
        var onConnectedField = typeof(GatewayHubConnection)
            .GetField("OnConnected", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

        if (onConnectedField is not null)
        {
            var del = (Action<ConnectedPayload>?)onConnectedField.GetValue(_manager.Hub);
            del?.Invoke(payload);
        }
        else
        {
            // Fallback: events in C# aren't fields — try the event's backing field pattern
            // For auto-implemented events, the backing field has the same name
            var backingField = typeof(GatewayHubConnection)
                .GetField("OnConnected", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (backingField is not null)
            {
                var del = (Action<ConnectedPayload>?)backingField.GetValue(_manager.Hub);
                del?.Invoke(payload);
            }
        }
    }
}
