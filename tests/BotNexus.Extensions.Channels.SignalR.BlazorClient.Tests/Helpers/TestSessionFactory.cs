using Bunit;
using Microsoft.Extensions.DependencyInjection;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

/// <summary>
/// Factory methods for building pre-configured <see cref="AgentSessionManager"/>
/// and <see cref="AgentSessionState"/> instances for bUnit tests.
/// </summary>
internal static class TestSessionFactory
{
    /// <summary>
    /// Creates an <see cref="AgentSessionManager"/> with a disconnected hub
    /// and a no-op <see cref="HttpClient"/>.
    /// </summary>
    public static AgentSessionManager CreateManager()
    {
        var hub = new GatewayHubConnection();
        var http = new HttpClient { BaseAddress = new Uri("http://localhost") };
        return new AgentSessionManager(hub, http);
    }

    /// <summary>
    /// Creates an <see cref="AgentSessionState"/> with sensible test defaults.
    /// </summary>
    public static AgentSessionState CreateAgentState(
        string agentId = "test-agent",
        string displayName = "Test Agent",
        string? sessionId = "sess-00000001",
        bool isConnected = true,
        bool isStreaming = false)
    {
        return new AgentSessionState
        {
            AgentId = agentId,
            DisplayName = displayName,
            SessionId = sessionId,
            IsConnected = isConnected,
            IsStreaming = isStreaming
        };
    }

    /// <summary>
    /// Creates an <see cref="AgentSessionState"/> with some pre-populated messages.
    /// </summary>
    public static AgentSessionState CreateAgentStateWithMessages(
        string agentId = "test-agent",
        string displayName = "Test Agent",
        bool isConnected = true,
        params (string Role, string Content)[] messages)
    {
        var state = CreateAgentState(agentId, displayName, isConnected: isConnected);

        // Set up a default conversation so Messages computed property returns the right store
        const string testConvId = "test-conv-1";
        state.ActiveConversationId = testConvId;
        var store = new System.Collections.Generic.List<ChatMessage>();
        state.ConversationMessageStores[testConvId] = store;

        foreach (var (role, content) in messages)
            store.Add(new ChatMessage(role, content, DateTimeOffset.UtcNow));

        return state;
    }

    /// <summary>
    /// Registers the <see cref="AgentSessionManager"/> as a singleton in the
    /// bUnit test context's service collection.
    /// </summary>
    public static void RegisterServices(
        BunitContext ctx,
        AgentSessionManager manager)
    {
        ctx.Services.AddSingleton(manager);
        ctx.Services.AddSingleton(manager.Hub);
    }
}
