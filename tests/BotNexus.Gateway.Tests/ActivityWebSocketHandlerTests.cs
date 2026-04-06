using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BotNexus.Gateway.Abstractions.Activity;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Api.WebSocket;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotNexus.Gateway.Tests;

public sealed class ActivityWebSocketHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithNonWebSocketRequest_ReturnsBadRequest()
    {
        var context = new DefaultHttpContext();
        var handler = new ActivityWebSocketHandler(new TestBroadcaster([]), NullLogger<ActivityWebSocketHandler>.Instance);

        await handler.HandleAsync(context, CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_WithAgentFilter_OnlyStreamsMatchingEvents()
    {
        var activities = new[]
        {
            new GatewayActivity { Type = GatewayActivityType.System, AgentId = "other", Message = "skip" },
            new GatewayActivity { Type = GatewayActivityType.System, AgentId = "agent-a", Message = "keep" }
        };

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?agent=agent-a");
        var socket = new TestWebSocket();
        context.Features.Set<IHttpWebSocketFeature>(new TestWebSocketFeature { IsWebSocketRequest = true, Socket = socket });
        var handler = new ActivityWebSocketHandler(new TestBroadcaster(activities), NullLogger<ActivityWebSocketHandler>.Instance);

        await handler.HandleAsync(context, CancellationToken.None);

        socket.SentMessages.Should().HaveCount(1);
        var payload = JsonDocument.Parse(Encoding.UTF8.GetString(socket.SentMessages[0]));
        payload.RootElement.GetProperty("agentId").GetString().Should().Be("agent-a");
        payload.RootElement.GetProperty("message").GetString().Should().Be("keep");
    }

    [Fact]
    public async Task HandleAsync_WhenStreamCompletes_ClosesSocketNormally()
    {
        var activities = new[]
        {
            new GatewayActivity { Type = GatewayActivityType.System, AgentId = "agent-a", Message = "done" }
        };

        var context = new DefaultHttpContext();
        var socket = new TestWebSocket();
        context.Features.Set<IHttpWebSocketFeature>(new TestWebSocketFeature { IsWebSocketRequest = true, Socket = socket });
        var handler = new ActivityWebSocketHandler(new TestBroadcaster(activities), NullLogger<ActivityWebSocketHandler>.Instance);

        await handler.HandleAsync(context, CancellationToken.None);

        socket.LastCloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
        socket.LastCloseDescription.Should().Be("Activity stream closed");
    }

    private sealed class TestBroadcaster(IEnumerable<GatewayActivity> activities) : IActivityBroadcaster
    {
        private readonly IReadOnlyList<GatewayActivity> _activities = activities.ToList();

        public ValueTask PublishAsync(GatewayActivity activity, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public async IAsyncEnumerable<GatewayActivity> SubscribeAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var activity in _activities)
            {
                yield return activity;
                await Task.Yield();
            }
        }
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

        public override void Abort()
            => _state = WebSocketState.Aborted;

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
            => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            SentMessages.Add(buffer.ToArray());
            return Task.CompletedTask;
        }

        public override void Dispose()
            => _state = WebSocketState.Closed;
    }
}
