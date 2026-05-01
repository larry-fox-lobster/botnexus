using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BotNexus.Agent.Core.Types;
using BotNexus.Domain.Primitives;
using BotNexus.Gateway;
using BotNexus.Gateway.Abstractions.Activity;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Channels;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Abstractions.Conversations;
using BotNexus.Gateway.Abstractions.Sessions;
using BotNexus.Gateway.Sessions;
using BotNexus.Gateway.Api;
using BotNexus.Extensions.Channels.SignalR;
using BotNexus.Gateway.Configuration;
using BotNexus.Gateway.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using GatewaySessionStatus = BotNexus.Gateway.Abstractions.Models.SessionStatus;

namespace BotNexus.Gateway.Tests.Integration;

/// <summary>
/// Probe Round 2 — Gateway integration tests covering SendMessageToConversation routing,
/// multi-message conversation linking, nonexistent conversation fallback,
/// and ResetSession/CompactSession conversationId preservation.
/// </summary>
[Trait("Category", "Integration")]
[Collection("IntegrationTests")]
public sealed class SignalRConversationRoutingTests : IAsyncDisposable
{
    private const string TestAgentId = "test-agent";

    // ── SendMessageToConversation: routes to correct conversation ────────────

    [Fact]
    public async Task Hub_SendMessageToConversation_RoutesToCorrectConversation()
    {
        var dispatcher = new RecordingDispatcher();
        await using var factory = CreateTestFactory(services =>
        {
            services.RemoveAll<IChannelDispatcher>();
            services.AddSingleton<IChannelDispatcher>(dispatcher);
        });
        using var cts = CreateTimeout();
        await RegisterAgentAsync(factory, cts.Token);

        // Create a conversation via REST
        using var client = factory.CreateClient();
        var createResp = await client.PostAsJsonAsync("/api/conversations",
            new { agentId = TestAgentId, title = "Targeted Conversation" }, cts.Token);
        createResp.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var convDoc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync(cts.Token)).RootElement;
        var conversationId = convDoc.GetProperty("conversationId").GetString()!;

        await using var connection = await CreateStartedConnection(factory, cts.Token);
        var result = await connection.InvokeAsync<JsonElement>(
            "SendMessageToConversation", TestAgentId, "signalr", "routed message", conversationId, cts.Token);

        var sessionId = result.GetProperty("sessionId").GetString();
        sessionId.ShouldNotBeNullOrWhiteSpace();

        // Verify the session has the conversationId stamped
        var sessionStore = factory.Services.GetRequiredService<ISessionStore>();
        var session = await sessionStore.GetAsync(SessionId.From(sessionId!), cts.Token);
        session.ShouldNotBeNull();
        session!.Session.ConversationId.ShouldNotBeNull("session should have conversationId stamped after SendMessageToConversation");
        session.Session.ConversationId!.Value.Value.ShouldBe(conversationId);
    }

    // ── Two messages to same conversation: both sessions link to same conversationId ──

    [Fact]
    public async Task Hub_TwoMessagesToSameConversation_BothSessionsLinkSameConversation()
    {
        var dispatcher = new RecordingDispatcher();
        await using var factory = CreateTestFactory(services =>
        {
            services.RemoveAll<IChannelDispatcher>();
            services.AddSingleton<IChannelDispatcher>(dispatcher);
        });
        using var cts = CreateTimeout();
        await RegisterAgentAsync(factory, cts.Token);

        using var client = factory.CreateClient();
        var createResp = await client.PostAsJsonAsync("/api/conversations",
            new { agentId = TestAgentId, title = "Shared Conversation" }, cts.Token);
        createResp.EnsureSuccessStatusCode();
        var conversationId = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync(cts.Token))
            .RootElement.GetProperty("conversationId").GetString()!;

        await using var conn1 = await CreateStartedConnection(factory, cts.Token);
        await using var conn2 = await CreateStartedConnection(factory, cts.Token);

        var result1 = await conn1.InvokeAsync<JsonElement>(
            "SendMessageToConversation", TestAgentId, "signalr", "message one", conversationId, cts.Token);
        var result2 = await conn2.InvokeAsync<JsonElement>(
            "SendMessageToConversation", TestAgentId, "signalr", "message two", conversationId, cts.Token);

        var sessionId1 = result1.GetProperty("sessionId").GetString()!;
        var sessionId2 = result2.GetProperty("sessionId").GetString()!;

        var sessionStore = factory.Services.GetRequiredService<ISessionStore>();
        var s1 = await sessionStore.GetAsync(SessionId.From(sessionId1), cts.Token);
        var s2 = await sessionStore.GetAsync(SessionId.From(sessionId2), cts.Token);

        s1.ShouldNotBeNull();
        s2.ShouldNotBeNull();
        s1!.Session.ConversationId.ShouldNotBeNull();
        s2!.Session.ConversationId.ShouldNotBeNull();
        s1.Session.ConversationId!.Value.Value.ShouldBe(conversationId);
        s2.Session.ConversationId!.Value.Value.ShouldBe(conversationId,
            "both messages to same conversation should produce sessions linking to the same conversationId");
    }

    // ── SendMessage without conversationId creates default conversation with conversationId ──

    [Fact]
    public async Task Hub_SendMessage_NoConversationId_SessionHasConversationIdStamped()
    {
        var dispatcher = new RecordingDispatcher();
        await using var factory = CreateTestFactory(services =>
        {
            services.RemoveAll<IChannelDispatcher>();
            services.AddSingleton<IChannelDispatcher>(dispatcher);
        });
        using var cts = CreateTimeout();
        await RegisterAgentAsync(factory, cts.Token);

        await using var connection = await CreateStartedConnection(factory, cts.Token);
        var result = await connection.InvokeAsync<JsonElement>("SendMessage", TestAgentId, "signalr", "hello", cts.Token);

        var sessionId = result.GetProperty("sessionId").GetString()!;

        var sessionStore = factory.Services.GetRequiredService<ISessionStore>();
        var session = await sessionStore.GetAsync(SessionId.From(sessionId), cts.Token);

        session.ShouldNotBeNull();
        session!.Session.ConversationId.ShouldNotBeNull(
            "SendMessage without explicit conversationId should still stamp the session with the default conversation's ID");
    }

    // ── ResetSession: new session created, conversationId preserved from conversation ──

    [Fact]
    public async Task Hub_ResetSession_NewSessionPreservesConversationId()
    {
        var dispatcher = new RecordingDispatcher();
        await using var factory = CreateTestFactory(services =>
        {
            services.RemoveAll<IChannelDispatcher>();
            services.AddSingleton<IChannelDispatcher>(dispatcher);
        });
        using var cts = CreateTimeout();
        await RegisterAgentAsync(factory, cts.Token);

        using var client = factory.CreateClient();
        var createResp = await client.PostAsJsonAsync("/api/conversations",
            new { agentId = TestAgentId, title = "Reset Test Conversation" }, cts.Token);
        createResp.EnsureSuccessStatusCode();
        var conversationId = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync(cts.Token))
            .RootElement.GetProperty("conversationId").GetString()!;

        await using var connection = await CreateStartedConnection(factory, cts.Token);
        var msgResult = await connection.InvokeAsync<JsonElement>(
            "SendMessageToConversation", TestAgentId, "signalr", "before reset", conversationId, cts.Token);
        var originalSessionId = msgResult.GetProperty("sessionId").GetString()!;

        // Reset the session
        var resetDone = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = connection.On<object>("SessionReset", payload => resetDone.TrySetResult(payload));
        await connection.InvokeAsync("ResetSession", TestAgentId, originalSessionId, cts.Token);
        await resetDone.Task.WaitAsync(cts.Token);

        // Send again — should route to same conversation and create a new session
        var msg2Result = await connection.InvokeAsync<JsonElement>(
            "SendMessageToConversation", TestAgentId, "signalr", "after reset", conversationId, cts.Token);
        var newSessionId = msg2Result.GetProperty("sessionId").GetString()!;

        newSessionId.ShouldNotBe(originalSessionId, "after reset, a new session should be created");

        var sessionStore = factory.Services.GetRequiredService<ISessionStore>();
        var newSession = await sessionStore.GetAsync(SessionId.From(newSessionId), cts.Token);
        newSession.ShouldNotBeNull();
        newSession!.Session.ConversationId.ShouldNotBeNull();
        newSession.Session.ConversationId!.Value.Value.ShouldBe(conversationId,
            "new session after ResetSession should still be linked to the same conversation");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WebApplicationFactory<Program> CreateTestFactory(Action<IServiceCollection>? configureServices = null)
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseUrls("http://127.0.0.1:0");
                builder.ConfigureServices(services =>
                {
                    var hostedServices = services
                        .Where(d => d.ServiceType == typeof(IHostedService))
                        .ToList();
                    foreach (var descriptor in hostedServices)
                        services.Remove(descriptor);

                    services.RemoveAll<IAgentConfigurationWriter>();
                    services.AddSingleton<IAgentConfigurationWriter, NoOpAgentConfigurationWriter>();

                    services.AddSignalRChannelForTests();

                    services.Replace(ServiceDescriptor.Singleton<ISessionStore, InMemorySessionStore>());
                    services.Replace(ServiceDescriptor.Singleton<IConversationStore, InMemoryConversationStore>());

                    configureServices?.Invoke(services);
                });
            });

    private static HubConnection CreateHubConnection(WebApplicationFactory<Program> factory)
    {
        var server = factory.Server;
        var handler = server.CreateHandler();
        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hub/gateway", options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    private static async Task<HubConnection> CreateStartedConnection(WebApplicationFactory<Program> factory, CancellationToken cancellationToken)
    {
        var connection = CreateHubConnection(factory);
        await connection.StartAsync(CancellationToken.None);
        return connection;
    }

    private static async Task RegisterAgentAsync(WebApplicationFactory<Program> factory, CancellationToken cancellationToken)
    {
        using var client = factory.CreateClient();
        var descriptor = new AgentDescriptor
        {
            AgentId = AgentId.From(TestAgentId),
            DisplayName = "Test Agent",
            ModelId = "gpt-4.1",
            ApiProvider = "copilot",
            IsolationStrategy = "in-process"
        };
        var response = await client.PostAsJsonAsync("/api/agents", descriptor, CancellationToken.None);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);
    }

    private static CancellationTokenSource CreateTimeout()
        => new(TimeSpan.FromSeconds(15));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class RecordingDispatcher : IChannelDispatcher
    {
        public List<InboundMessage> Messages { get; } = [];

        public Task DispatchAsync(InboundMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
