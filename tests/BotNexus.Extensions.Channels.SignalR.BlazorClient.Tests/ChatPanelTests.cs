using Bunit;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Components;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests;

/// <summary>
/// bUnit tests for the <see cref="ChatPanel"/> component.
/// </summary>
public sealed class ChatPanelTests : IDisposable
{
    private readonly BunitContext _ctx = new();
    private readonly AgentSessionManager _manager;

    public ChatPanelTests()
    {
        _manager = TestSessionFactory.CreateManager();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _manager.Dispose();
        _ctx.Dispose();
    }

    // ── Rendering messages ────────────────────────────────────────────────

    [Fact]
    public void Renders_messages_from_state()
    {
        var state = TestSessionFactory.CreateAgentStateWithMessages(
            messages: [
                ("User", "Hello there!"),
                ("Assistant", "Hi! How can I help?")
            ]);

        var cut = RenderChatPanel(state);

        // Both User and Assistant messages should be rendered
        var messages = cut.FindAll(".message:not(.tool)");
        messages.Should().HaveCountGreaterThanOrEqualTo(2);

        // User message content is rendered directly (no markdown)
        cut.Markup.Should().Contain("Hello there!");

        // Assistant message exists — content goes through JS markdown rendering
        // which returns empty in test (bUnit loose JS mock), but the element exists
        var assistantMsgs = cut.FindAll(".message.assistant");
        assistantMsgs.Should().NotBeEmpty();
    }

    [Fact]
    public void Shows_message_roles()
    {
        var state = TestSessionFactory.CreateAgentStateWithMessages(
            messages: [
                ("User", "test message"),
                ("Assistant", "response")
            ]);

        var cut = RenderChatPanel(state);

        var roles = cut.FindAll(".message-role");
        roles.Should().Contain(r => r.TextContent == "User");
        roles.Should().Contain(r => r.TextContent == "Assistant");
    }

    [Fact]
    public void Shows_timestamps_on_messages()
    {
        var state = TestSessionFactory.CreateAgentStateWithMessages(
            messages: [("User", "Hello")]);

        var cut = RenderChatPanel(state);

        var timeElements = cut.FindAll(".message-time");
        timeElements.Should().NotBeEmpty();
        // Timestamp should not be empty
        timeElements[0].TextContent.Should().NotBeNullOrWhiteSpace();
    }

    // ── Streaming indicator ──────────────────────────────────────────────

    [Fact]
    public void Shows_streaming_badge_when_IsStreaming()
    {
        var state = TestSessionFactory.CreateAgentState(isStreaming: true);
        state.CurrentStreamBuffer = "Thinking...";

        var cut = RenderChatPanel(state);

        cut.Find(".streaming-badge").TextContent.Should().Contain("Streaming");
    }

    [Fact]
    public void Shows_streaming_indicator_dot_when_streaming_with_buffer()
    {
        var state = TestSessionFactory.CreateAgentState(isStreaming: true);
        state.CurrentStreamBuffer = "partial response";

        var cut = RenderChatPanel(state);

        cut.Find(".streaming-indicator").TextContent.Should().Contain("●");
    }

    [Fact]
    public void Shows_streaming_message_content_from_buffer()
    {
        var state = TestSessionFactory.CreateAgentState(isStreaming: true);
        state.CurrentStreamBuffer = "The answer is 42";

        var cut = RenderChatPanel(state);

        cut.Find(".message.assistant.streaming .message-content")
            .TextContent.Should().Contain("The answer is 42");
    }

    [Fact]
    public void Does_not_show_streaming_indicator_when_not_streaming()
    {
        var state = TestSessionFactory.CreateAgentState(isStreaming: false);

        var cut = RenderChatPanel(state);

        cut.FindAll(".streaming-badge").Should().BeEmpty();
        cut.FindAll(".streaming-indicator").Should().BeEmpty();
    }

    // ── Tool calls ───────────────────────────────────────────────────────

    [Fact]
    public void Shows_tool_calls_with_tool_name()
    {
        var state = TestSessionFactory.CreateAgentState();
        state.WithToolCall(toolName: "search_code", toolResult: "3 results");

        var cut = RenderChatPanel(state);

        cut.Find(".tool-name").TextContent.Should().Contain("search_code");
    }

    [Fact]
    public void Tool_details_collapsed_by_default()
    {
        var state = TestSessionFactory.CreateAgentState();
        state.WithToolCall();

        var cut = RenderChatPanel(state);

        cut.FindAll(".tool-details").Should().BeEmpty();
        cut.Find(".tool-expand").TextContent.Should().Contain("▸");
    }

    [Fact]
    public void Clicking_tool_header_expands_details()
    {
        var state = TestSessionFactory.CreateAgentState();
        state.WithToolCall(
            toolArgs: """{"query": "test"}""",
            toolResult: "Found 5 matches.");

        var cut = RenderChatPanel(state);

        // Click to expand
        cut.Find(".tool-header").Click();

        cut.Find(".tool-details").Should().NotBeNull();
        cut.Find(".tool-expand").TextContent.Should().Contain("▾");
        cut.Markup.Should().Contain("Arguments");
        cut.Markup.Should().Contain("Result");
    }

    [Fact]
    public void Clicking_tool_header_twice_collapses_details()
    {
        var state = TestSessionFactory.CreateAgentState();
        state.WithToolCall();

        var cut = RenderChatPanel(state);

        // Expand
        cut.Find(".tool-header").Click();
        cut.FindAll(".tool-details").Should().NotBeEmpty();

        // Collapse
        cut.Find(".tool-header").Click();
        cut.FindAll(".tool-details").Should().BeEmpty();
    }

    [Fact]
    public void Shows_tool_duration_when_available()
    {
        var state = TestSessionFactory.CreateAgentState();
        state.WithToolCall(duration: TimeSpan.FromSeconds(1.5));

        var cut = RenderChatPanel(state);

        cut.Find(".tool-duration").TextContent.Should().Contain("1.5s");
    }

    [Fact]
    public void Shows_tool_duration_in_milliseconds_for_fast_calls()
    {
        var state = TestSessionFactory.CreateAgentState();
        state.WithToolCall(duration: TimeSpan.FromMilliseconds(350));

        var cut = RenderChatPanel(state);

        cut.Find(".tool-duration").TextContent.Should().Contain("350ms");
    }

    [Fact]
    public void Shows_pending_tool_icon()
    {
        var state = TestSessionFactory.CreateAgentState();
        state.WithPendingToolCall(toolName: "run_query");

        var cut = RenderChatPanel(state);

        cut.Find(".tool-icon").TextContent.Should().Contain("⏳");
    }

    [Fact]
    public void Shows_error_tool_styling()
    {
        var state = TestSessionFactory.CreateAgentState();
        state.WithToolCall(toolName: "compile", isError: true, toolResult: "Build failed");

        var cut = RenderChatPanel(state);

        cut.Find(".message.tool.tool-error").Should().NotBeNull();
        cut.Find(".tool-icon").TextContent.Should().Contain("❌");
    }

    // ── Send button ──────────────────────────────────────────────────────

    [Fact]
    public void Send_button_disabled_when_not_connected()
    {
        var state = TestSessionFactory.CreateAgentState(isConnected: false);

        var cut = RenderChatPanel(state);

        var sendBtn = cut.Find(".send-btn");
        sendBtn.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Send_button_disabled_when_input_is_empty()
    {
        var state = TestSessionFactory.CreateAgentState(isConnected: true);

        var cut = RenderChatPanel(state);

        var sendBtn = cut.Find(".send-btn");
        sendBtn.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Textarea_disabled_when_not_connected()
    {
        var state = TestSessionFactory.CreateAgentState(isConnected: false);

        var cut = RenderChatPanel(state);

        var textarea = cut.Find(".chat-input");
        textarea.HasAttribute("disabled").Should().BeTrue();
    }

    // ── Streaming mode buttons ───────────────────────────────────────────

    [Fact]
    public void Shows_steer_and_stop_buttons_when_streaming()
    {
        var state = TestSessionFactory.CreateAgentState(isStreaming: true);

        var cut = RenderChatPanel(state);

        cut.FindAll(".steer-btn").Should().NotBeEmpty();
        cut.FindAll(".abort-btn").Should().NotBeEmpty();
        cut.FindAll(".send-btn").Should().BeEmpty();
    }

    [Fact]
    public void Shows_send_button_when_not_streaming()
    {
        var state = TestSessionFactory.CreateAgentState(isStreaming: false);

        var cut = RenderChatPanel(state);

        cut.FindAll(".send-btn").Should().NotBeEmpty();
        cut.FindAll(".steer-btn").Should().BeEmpty();
        cut.FindAll(".abort-btn").Should().BeEmpty();
    }

    // ── Header ───────────────────────────────────────────────────────────

    [Fact]
    public void Shows_agent_display_name_in_header()
    {
        var state = TestSessionFactory.CreateAgentState(displayName: "Nova Assistant");

        var cut = RenderChatPanel(state);

        cut.Find(".chat-header h3").TextContent.Should().Contain("Nova Assistant");
    }

    [Fact]
    public void Shows_agent_id_in_header()
    {
        var state = TestSessionFactory.CreateAgentState(agentId: "nova-v2");

        var cut = RenderChatPanel(state);

        cut.Find(".agent-id-label").TextContent.Should().Contain("nova-v2");
    }

    // ── Loading history ──────────────────────────────────────────────────

    [Fact]
    public void Shows_loading_spinner_when_loading_history()
    {
        var state = TestSessionFactory.CreateAgentState();
        state.IsLoadingHistory = true;

        var cut = RenderChatPanel(state);

        cut.Find(".history-loading").Should().NotBeNull();
        cut.Markup.Should().Contain("Loading history");
    }

    [Fact]
    public void Does_not_show_loading_spinner_when_not_loading()
    {
        var state = TestSessionFactory.CreateAgentState();
        state.IsLoadingHistory = false;

        var cut = RenderChatPanel(state);

        cut.FindAll(".history-loading").Should().BeEmpty();
    }

    // ── Placeholder text ─────────────────────────────────────────────────

    [Fact]
    public void Shows_steer_placeholder_when_streaming()
    {
        var state = TestSessionFactory.CreateAgentState(isStreaming: true);

        var cut = RenderChatPanel(state);

        var textarea = cut.Find(".chat-input");
        textarea.GetAttribute("placeholder").Should().Contain("steer");
    }

    [Fact]
    public void Shows_message_placeholder_when_not_streaming()
    {
        var state = TestSessionFactory.CreateAgentState(isStreaming: false);

        var cut = RenderChatPanel(state);

        var textarea = cut.Find(".chat-input");
        textarea.GetAttribute("placeholder").Should().Contain("Type a message");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private IRenderedComponent<ChatPanel> RenderChatPanel(AgentSessionState state)
    {
        return _ctx.Render<ChatPanel>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Manager, _manager));
    }
}
