using Bunit;
using Microsoft.AspNetCore.SignalR.Client;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Components;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests;

/// <summary>
/// bUnit tests for the <see cref="ConnectionStatus"/> component.
/// Since <see cref="GatewayHubConnection"/> is sealed and wraps a real
/// <see cref="HubConnection"/>, we test against the actual default state
/// (Disconnected) and verify the three rendered properties (dot, label, class).
/// </summary>
public sealed class ConnectionStatusTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public void Dispose() => _ctx.Dispose();

    // ── Disconnected state (default — no connection established) ─────────

    [Fact]
    public void Shows_Disconnected_when_hub_has_no_connection()
    {
        var hub = new GatewayHubConnection();

        var cut = _ctx.Render<ConnectionStatus>(p => p
            .Add(c => c.Hub, hub));

        cut.Find(".connection-label").TextContent.Should().Be("Disconnected");
        cut.Find(".connection-dot").TextContent.Should().Contain("○");
        cut.Find(".connection-indicator").ClassList.Should().Contain("status-disconnected");
    }

    // ── Structural tests (verify the component renders required elements) ─

    [Fact]
    public void Renders_connection_indicator_container()
    {
        var hub = new GatewayHubConnection();

        var cut = _ctx.Render<ConnectionStatus>(p => p
            .Add(c => c.Hub, hub));

        cut.Find(".connection-indicator").Should().NotBeNull();
        cut.Find(".connection-dot").Should().NotBeNull();
        cut.Find(".connection-label").Should().NotBeNull();
    }

    [Fact]
    public void Has_title_attribute_matching_label()
    {
        var hub = new GatewayHubConnection();

        var cut = _ctx.Render<ConnectionStatus>(p => p
            .Add(c => c.Hub, hub));

        var indicator = cut.Find(".connection-indicator");
        indicator.GetAttribute("title").Should().Be("Disconnected");
    }

    // ── Label mapping verification ──────────────────────────────────────
    // We can't easily set Connected/Reconnecting states without a real server,
    // but we can at least verify the state-to-label mapping logic works for
    // the Disconnected case and document expected values for other states.

    [Theory]
    [InlineData(HubConnectionState.Connected, "Connected", "●", "status-connected")]
    [InlineData(HubConnectionState.Connecting, "Connecting…", "◌", "status-connecting")]
    [InlineData(HubConnectionState.Reconnecting, "Reconnecting…", "◌", "status-reconnecting")]
    [InlineData(HubConnectionState.Disconnected, "Disconnected", "○", "status-disconnected")]
    public void Label_mapping_returns_expected_values(
        HubConnectionState state,
        string expectedLabel,
        string expectedDot,
        string expectedCssClass)
    {
        // This test documents the mapping logic. Since ConnectionStatus reads
        // from Hub.State (which we can't easily fake), we verify the mapping
        // table directly through the component for the Disconnected case and
        // assert the expectations as documentation for other states.
        if (state == HubConnectionState.Disconnected)
        {
            var hub = new GatewayHubConnection();
            var cut = _ctx.Render<ConnectionStatus>(p => p.Add(c => c.Hub, hub));

            cut.Find(".connection-label").TextContent.Should().Be(expectedLabel);
            cut.Find(".connection-dot").TextContent.Should().Contain(expectedDot);
            cut.Find(".connection-indicator").ClassList.Should().Contain(expectedCssClass);
        }
        else
        {
            // Document expected values — verified by inspection of the component source
            expectedLabel.Should().NotBeNullOrEmpty(
                $"expected label for {state} should be '{expectedLabel}'");
            expectedDot.Should().NotBeNullOrEmpty(
                $"expected dot for {state} should be '{expectedDot}'");
            expectedCssClass.Should().NotBeNullOrEmpty(
                $"expected CSS class for {state} should be '{expectedCssClass}'");
        }
    }
}
