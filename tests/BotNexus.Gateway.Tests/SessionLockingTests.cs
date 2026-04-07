using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BotNexus.Channels.WebSocket;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Sessions;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Api.WebSocket;
using BotNexus.Gateway.Sessions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BotNexus.Gateway.Tests;

public sealed class SessionLockingTests
{
    [Fact]
    public async Task WebSocketHandler_RejectsSecondConnection_ToSameSession()
    {
        var store = new BlockingGetOrCreateSessionStore();
        var handler = CreateHandler(store);

        var firstSocket = new TestWebSocket();
        var firstContext = CreateWebSocketContext("agent-a", "session-1", firstSocket);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var firstConnectionTask = handler.HandleAsync(firstContext, cts.Token);
        await store.WaitUntilBlockedAsync(cts.Token);

        var secondSocket = new TestWebSocket();
        var secondContext = CreateWebSocketContext("agent-a", "session-1", secondSocket);

        await handler.HandleAsync(secondContext, cts.Token);

        secondSocket.LastCloseStatus.Should().NotBe((WebSocketCloseStatus)4409);
        var payloads = ParsePayloads(secondSocket.SentMessages);
        payloads.Any(payload =>
            payload.TryGetProperty("type", out var type) &&
            string.Equals(type.GetString(), "error", StringComparison.Ordinal) &&
            payload.TryGetProperty("code", out var code) &&
            string.Equals(code.GetString(), "SESSION_ALREADY_CONNECTED", StringComparison.Ordinal))
            .Should().BeTrue();

        store.Release();
        await firstConnectionTask;
    }

    [Fact]
    public async Task WebSocketHandler_AllowsConnection_ToDifferentSession()
    {
        var store = new BlockingGetOrCreateSessionStore();
        var handler = CreateHandler(store);

        var firstSocket = new TestWebSocket();
        var firstContext = CreateWebSocketContext("agent-a", "session-1", firstSocket);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var firstConnectionTask = handler.HandleAsync(firstContext, cts.Token);
        await store.WaitUntilBlockedAsync(cts.Token);

        var secondSocket = new TestWebSocket();
        var secondContext = CreateWebSocketContext("agent-a", "session-2", secondSocket);

        await handler.HandleAsync(secondContext, cts.Token);

        secondSocket.LastCloseStatus.Should().NotBe((WebSocketCloseStatus)4409);
        secondSocket.SentMessages.Should().NotBeEmpty();

        var payloads = ParsePayloads(secondSocket.SentMessages);
        payloads.Any(payload =>
            payload.TryGetProperty("type", out var type) &&
            string.Equals(type.GetString(), "session_switched", StringComparison.Ordinal) &&
            payload.TryGetProperty("sessionId", out var sessionId) &&
            string.Equals(sessionId.GetString(), "session-2", StringComparison.Ordinal))
            .Should().BeTrue();

        store.Release();
        await firstConnectionTask;
    }

    [Fact]
    public async Task WebSocketHandler_ReleasesLock_OnDisconnect()
    {
        var handler = CreateHandler();

        var firstSocket = new TestWebSocket();
        var firstContext = CreateWebSocketContext("agent-a", "session-1", firstSocket);

        await handler.HandleAsync(firstContext, CancellationToken.None);

        var reconnectSocket = new TestWebSocket();
        var reconnectContext = CreateWebSocketContext("agent-a", "session-1", reconnectSocket);

        await handler.HandleAsync(reconnectContext, CancellationToken.None);

        reconnectSocket.LastCloseStatus.Should().NotBe((WebSocketCloseStatus)4409);
        ParsePayloads(reconnectSocket.SentMessages)
            .Any(payload =>
                payload.TryGetProperty("type", out var type) &&
                string.Equals(type.GetString(), "session_switched", StringComparison.Ordinal) &&
                payload.TryGetProperty("sessionId", out var sessionId) &&
                string.Equals(sessionId.GetString(), "session-1", StringComparison.Ordinal))
            .Should().BeTrue();
    }

    [Fact]
    public async Task WebSocketHandler_AllowsReconnect_AfterDisconnect()
    {
        var handler = CreateHandler();

        var firstSocket = new TestWebSocket();
        var firstContext = CreateWebSocketContext("agent-a", "session-1", firstSocket);
        await handler.HandleAsync(firstContext, CancellationToken.None);

        var reconnectSocket = new TestWebSocket();
        var reconnectContext = CreateWebSocketContext("agent-a", "session-1", reconnectSocket);
        await handler.HandleAsync(reconnectContext, CancellationToken.None);

        reconnectSocket.LastCloseStatus.Should().NotBe((WebSocketCloseStatus)4409);
        ParsePayloads(reconnectSocket.SentMessages)
            .Any(payload =>
                payload.TryGetProperty("type", out var type) &&
                string.Equals(type.GetString(), "session_switched", StringComparison.Ordinal) &&
                payload.TryGetProperty("sessionId", out var sessionId) &&
                string.Equals(sessionId.GetString(), "session-1", StringComparison.Ordinal))
            .Should().BeTrue();
    }

    private static GatewayWebSocketHandler CreateHandler(ISessionStore? sessions = null)
    {
        var options = Options.Create(new GatewayWebSocketOptions());
        var channelAdapter = new WebSocketChannelAdapter(NullLogger<WebSocketChannelAdapter>.Instance);
        var resolvedSessions = sessions ?? new InMemorySessionStore();
        var registry = CreateAgentRegistry();
        var connectionManager = new WebSocketConnectionManager(options, NullLogger<WebSocketConnectionManager>.Instance);
        var dispatcher = new WebSocketMessageDispatcher(
            Mock.Of<IAgentSupervisor>(),
            registry.Object,
            channelAdapter,
            resolvedSessions,
            options,
            connectionManager,
            NullLogger<WebSocketMessageDispatcher>.Instance);

        return new GatewayWebSocketHandler(
            channelAdapter,
            options,
            connectionManager,
            dispatcher,
            NullLogger<GatewayWebSocketHandler>.Instance);
    }

    private static List<JsonElement> ParsePayloads(IEnumerable<byte[]> sentMessages)
        => sentMessages
            .Select(bytes => JsonDocument.Parse(Encoding.UTF8.GetString(bytes)).RootElement.Clone())
            .ToList();

    private static Mock<IAgentRegistry> CreateAgentRegistry()
    {
        var registry = new Mock<IAgentRegistry>();
        var agents = new[]
        {
            new AgentDescriptor
            {
                AgentId = "agent-a",
                DisplayName = "Agent A",
                ModelId = "test-model",
                ApiProvider = "test"
            }
        };

        registry.Setup(r => r.GetAll()).Returns(agents);
        registry.Setup(r => r.Contains(It.IsAny<string>()))
            .Returns((string agentId) => agents.Any(agent => string.Equals(agent.AgentId, agentId, StringComparison.OrdinalIgnoreCase)));
        return registry;
    }

    private static HttpContext CreateWebSocketContext(string agentId, string sessionId, WebSocket socket)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?agent={agentId}&session={sessionId}");
        context.Features.Set<IHttpWebSocketFeature>(new TestWebSocketFeature
        {
            IsWebSocketRequest = true,
            Socket = socket
        });
        return context;
    }

    private sealed class TestWebSocketFeature : IHttpWebSocketFeature
    {
        public required WebSocket Socket { get; init; }

        public bool IsWebSocketRequest { get; init; }

        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
            => Task.FromResult(Socket);
    }

    private sealed class TestWebSocket : WebSocket
    {
        private WebSocketState _state = WebSocketState.Open;

        public List<byte[]> SentMessages { get; } = [];
        public WebSocketCloseStatus? LastCloseStatus { get; private set; }
        public string? LastCloseDescription { get; private set; }

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

        public override void Abort() => _state = WebSocketState.Aborted;

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            LastCloseStatus = closeStatus;
            LastCloseDescription = statusDescription;
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseReceived;
            return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            SentMessages.Add(buffer.ToArray());
            return Task.CompletedTask;
        }

        public override void Dispose() => _state = WebSocketState.Closed;
    }

    private sealed class BlockingGetOrCreateSessionStore : ISessionStore
    {
        private readonly InMemorySessionStore _inner = new();
        private readonly TaskCompletionSource<bool> _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _shouldBlock = 1;

        public Task WaitUntilBlockedAsync(CancellationToken cancellationToken)
            => _blocked.Task.WaitAsync(cancellationToken);

        public void Release()
            => _release.TrySetResult(true);

        public Task<GatewaySession?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
            => _inner.GetAsync(sessionId, cancellationToken);

        public async Task<GatewaySession> GetOrCreateAsync(string sessionId, string agentId, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _shouldBlock, 0) == 1)
            {
                _blocked.TrySetResult(true);
                await _release.Task.WaitAsync(cancellationToken);
            }

            return await _inner.GetOrCreateAsync(sessionId, agentId, cancellationToken);
        }

        public Task SaveAsync(GatewaySession session, CancellationToken cancellationToken = default)
            => _inner.SaveAsync(session, cancellationToken);

        public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
            => _inner.DeleteAsync(sessionId, cancellationToken);

        public Task<IReadOnlyList<GatewaySession>> ListAsync(string? agentId = null, CancellationToken cancellationToken = default)
            => _inner.ListAsync(agentId, cancellationToken);
    }
}

