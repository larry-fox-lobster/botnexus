using Microsoft.AspNetCore.SignalR.Client;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

/// <summary>
/// Lightweight fake for <see cref="GatewayHubConnection"/> used in bUnit tests.
/// Since <see cref="GatewayHubConnection"/> is sealed and wraps a real
/// <see cref="HubConnection"/>, we can't mock it with NSubstitute.
/// Instead, we create real instances and accept that <see cref="GatewayHubConnection.State"/>
/// will report <see cref="HubConnectionState.Disconnected"/> (the default when no
/// connection has been established).
///
/// For tests that need specific connection states, use <see cref="FakeGatewayHub"/>
/// which provides settable state.
/// </summary>
internal static class TestHubFactory
{
    /// <summary>
    /// Creates a real <see cref="GatewayHubConnection"/> in the default (disconnected) state.
    /// Suitable for tests that don't depend on connection status.
    /// </summary>
    public static GatewayHubConnection CreateDisconnected() => new();
}

/// <summary>
/// A thin wrapper that presents a controllable hub-like interface for tests
/// that need to simulate specific <see cref="HubConnectionState"/> values.
/// We use this to set up the manager and sessions without hitting a real hub.
/// </summary>
internal sealed class FakeGatewayHub
{
    public GatewayHubConnection Real { get; } = new();

    /// <summary>
    /// The real hub will always report <see cref="HubConnectionState.Disconnected"/>
    /// because we never call <see cref="GatewayHubConnection.ConnectAsync"/>.
    /// Tests that need "connected" state should set <see cref="AgentSessionState.IsConnected"/>
    /// directly on the session state instead.
    /// </summary>
    public HubConnectionState State => Real.State;
}
