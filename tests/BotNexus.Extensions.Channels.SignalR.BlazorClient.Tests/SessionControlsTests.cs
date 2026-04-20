using Bunit;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Components;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests;

/// <summary>
/// bUnit tests for the <see cref="SessionControls"/> component.
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

    // ── Session ID display ───────────────────────────────────────────────

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

        cut.Find(".session-id").GetAttribute("title").ShouldBe("Click to copy: abcdef1234567890");
    }

    [Fact]
    public void Does_not_render_controls_when_no_session_id()
    {
        var state = TestSessionFactory.CreateAgentState(sessionId: null);

        var cut = RenderSessionControls(state);

        cut.FindAll(".session-id").ShouldBeEmpty();
        cut.FindAll(".reset-btn").ShouldBeEmpty();
    }

    // ── Reset button ─────────────────────────────────────────────────────

    [Fact]
    public void Reset_button_is_present_when_session_exists()
    {
        var state = TestSessionFactory.CreateAgentState(sessionId: "test-session-123");

        var cut = RenderSessionControls(state);

        var resetBtn = cut.Find(".reset-btn");
        resetBtn.ShouldNotBeNull();
        resetBtn.TextContent.ShouldContain("Reset");
    }

    // ── Reset confirmation dialog ────────────────────────────────────────

    [Fact]
    public void Confirmation_dialog_not_shown_initially()
    {
        var state = TestSessionFactory.CreateAgentState();

        var cut = RenderSessionControls(state);

        cut.FindAll(".reset-confirm-overlay").ShouldBeEmpty();
    }

    [Fact]
    public void Clicking_reset_shows_confirmation_dialog()
    {
        var state = TestSessionFactory.CreateAgentState(displayName: "Nova");

        var cut = RenderSessionControls(state);

        cut.Find(".reset-btn").Click();

        cut.Find(".reset-confirm-dialog").ShouldNotBeNull();
        cut.Markup.ShouldContain("Reset session for");
        cut.Markup.ShouldContain("Nova");
    }

    [Fact]
    public void Confirmation_dialog_has_cancel_and_confirm_buttons()
    {
        var state = TestSessionFactory.CreateAgentState();
        var cut = RenderSessionControls(state);

        cut.Find(".reset-btn").Click();

        cut.Find(".cancel-btn").ShouldNotBeNull();
        cut.Find(".confirm-btn").ShouldNotBeNull();
    }

    [Fact]
    public void Clicking_cancel_hides_confirmation_dialog()
    {
        var state = TestSessionFactory.CreateAgentState();
        var cut = RenderSessionControls(state);

        // Open the dialog
        cut.Find(".reset-btn").Click();
        cut.FindAll(".reset-confirm-overlay").ShouldNotBeEmpty();

        // Cancel
        cut.Find(".cancel-btn").Click();
        cut.FindAll(".reset-confirm-overlay").ShouldBeEmpty();
    }

    [Fact]
    public void Clicking_overlay_hides_confirmation_dialog()
    {
        var state = TestSessionFactory.CreateAgentState();
        var cut = RenderSessionControls(state);

        // Open the dialog
        cut.Find(".reset-btn").Click();
        cut.FindAll(".reset-confirm-overlay").ShouldNotBeEmpty();

        // Click overlay background
        cut.Find(".reset-confirm-overlay").Click();
        cut.FindAll(".reset-confirm-overlay").ShouldBeEmpty();
    }

    [Fact]
    public void Confirmation_dialog_mentions_clearing_messages()
    {
        var state = TestSessionFactory.CreateAgentState();
        var cut = RenderSessionControls(state);

        cut.Find(".reset-btn").Click();

        cut.Markup.ShouldContain("clear all messages");
    }

    // ── Short session IDs ────────────────────────────────────────────────

    [Fact]
    public void Handles_short_session_id_without_error()
    {
        var state = TestSessionFactory.CreateAgentState(sessionId: "abc");

        var cut = RenderSessionControls(state);

        var sessionId = cut.Find(".session-id");
        sessionId.TextContent.ShouldContain("abc");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private IRenderedComponent<SessionControls> RenderSessionControls(AgentSessionState state)
    {
        return _ctx.Render<SessionControls>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Manager, _manager));
    }
}
