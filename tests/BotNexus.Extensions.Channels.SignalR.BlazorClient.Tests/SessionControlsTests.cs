using Bunit;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Components;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests;

/// <summary>
/// bUnit tests for the <see cref="SessionControls"/> component.
/// SessionControls now only shows the session ID copy badge — the reset/new-session
/// button has moved to ChatPanel header.
/// </summary>
public sealed class SessionControlsTests : IDisposable
{
    private readonly BunitContext _ctx = new();
    private readonly AgentSessionManager _manager;

    public SessionControlsTests()
    {
        _manager = TestSessionFactory.CreateManager();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _manager.Dispose();
        _ctx.Dispose();
    }

    [Fact]
    public void Shows_truncated_session_id()
    {
        var state = TestSessionFactory.CreateAgentState(sessionId: "abcdef1234567890");

        var cut = RenderSessionControls(state);

        var sessionId = cut.Find(".session-id");
        sessionId.TextContent.ShouldContain("abcdef12");
        sessionId.TextContent.ShouldContain("…");
    }

    [Fact]
    public void Shows_full_session_id_in_title_attribute()
    {
        var state = TestSessionFactory.CreateAgentState(sessionId: "abcdef1234567890");

        var cut = RenderSessionControls(state);

        cut.Find(".session-id").GetAttribute("title").ShouldContain("abcdef1234567890");
    }

    [Fact]
    public void Does_not_render_session_id_when_no_session()
    {
        var state = TestSessionFactory.CreateAgentState(sessionId: null);

        var cut = RenderSessionControls(state);

        cut.FindAll(".session-id").ShouldBeEmpty();
    }

    [Fact]
    public void Reset_button_is_not_present_in_session_controls()
    {
        // Reset/new-session moved to ChatPanel header — not in SessionControls
        var state = TestSessionFactory.CreateAgentState(sessionId: "test-session-123");

        var cut = RenderSessionControls(state);

        cut.FindAll(".reset-btn").ShouldBeEmpty();
    }

    [Fact]
    public void Handles_short_session_id_without_error()
    {
        var state = TestSessionFactory.CreateAgentState(sessionId: "abc");

        var cut = RenderSessionControls(state);

        cut.Find(".session-id").TextContent.ShouldContain("abc");
    }

    private IRenderedComponent<SessionControls> RenderSessionControls(AgentSessionState state)
    {
        return _ctx.Render<SessionControls>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Manager, _manager));
    }
}
