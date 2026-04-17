using BotNexus.Channels.SignalR;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Domain.Primitives;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BotNexus.Gateway.Tests;

public sealed class SignalRChannelAdapterTests
{
    [Fact]
    public async Task SendStreamEventAsync_WhitespaceSessionId_UsesNormalizedGroupAndPayload()
    {
        var clientProxy = new Mock<IClientProxy>();
        clientProxy.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients.Setup(value => value.Group("session:session-1")).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<GatewayHub>>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var adapter = new SignalRChannelAdapter(NullLogger<SignalRChannelAdapter>.Instance, hubContext.Object);
        var streamEvent = new AgentStreamEvent { Type = AgentStreamEventType.ContentDelta, ContentDelta = "delta" };

        await adapter.SendStreamEventAsync("  session-1  ", streamEvent, CancellationToken.None);

        clients.Verify(value => value.Group("session:session-1"), Times.Once);
        clientProxy.Verify(proxy => proxy.SendCoreAsync(
                "ContentDelta",
                It.Is<object?[]>(args =>
                    args.Length == 1 &&
                    args[0] is AgentStreamEvent &&
                    ((AgentStreamEvent)args[0]!).SessionId == SessionId.From("session-1")),
                CancellationToken.None),
            Times.Once);
    }
}
