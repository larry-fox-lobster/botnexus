using BotNexus.Domain.Primitives;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Channels;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Abstractions.Sessions;
using BotNexus.Gateway.Abstractions.Triggers;
using BotNexus.Gateway.Api.Hubs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BotNexus.Gateway.Tests.Api;

public sealed class CronTriggerTests
{
    [Fact]
    public void CronTrigger_ImplementsInternalTrigger_NotChannelAdapter()
    {
        typeof(IInternalTrigger).IsAssignableFrom(typeof(CronTrigger)).Should().BeTrue();
        typeof(IChannelAdapter).IsAssignableFrom(typeof(CronTrigger)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateSessionAsync_CreatesCronSession_WithCronTriggerType()
    {
        var sessionStore = new Mock<ISessionStore>();
        var supervisor = new Mock<IAgentSupervisor>();
        var handle = new Mock<IAgentHandle>();
        GatewaySession? savedSession = null;

        handle.SetupGet(h => h.AgentId).Returns(AgentId.From("agent-a"));
        handle.SetupGet(h => h.SessionId).Returns(SessionId.From("session-a"));
        handle.SetupGet(h => h.IsRunning).Returns(true);
        handle.Setup(h => h.PromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse { Content = "cron-response" });

        sessionStore
            .Setup(s => s.GetOrCreateAsync(It.IsAny<SessionId>(), AgentId.From("agent-a"), It.IsAny<CancellationToken>()))
            .Returns<SessionId, AgentId, CancellationToken>((sessionId, agentId, _) => Task.FromResult(new GatewaySession
            {
                SessionId = sessionId,
                AgentId = agentId
            }));
        sessionStore
            .Setup(s => s.SaveAsync(It.IsAny<GatewaySession>(), It.IsAny<CancellationToken>()))
            .Callback<GatewaySession, CancellationToken>((session, _) => savedSession = session)
            .Returns(Task.CompletedTask);
        supervisor
            .Setup(s => s.GetOrCreateAsync(AgentId.From("agent-a"), It.IsAny<SessionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handle.Object);

        var trigger = new CronTrigger(supervisor.Object, sessionStore.Object, NullLogger<CronTrigger>.Instance);
        var sessionId = await trigger.CreateSessionAsync(AgentId.From("agent-a"), "Run scheduled task");

        trigger.Type.Should().Be(TriggerType.Cron);
        sessionId.Value.Should().StartWith("cron:");
        savedSession.Should().NotBeNull();
        savedSession!.SessionType.Should().Be(SessionType.Cron);
        savedSession.ChannelType.Should().Be(ChannelKey.From("cron"));
        savedSession.CallerId.Should().Be("cron:agent-a");
        savedSession.History.Should().ContainSingle(e => e.Role == MessageRole.User && e.Content == "Run scheduled task");
        savedSession.History.Should().ContainSingle(e => e.Role == MessageRole.Assistant && e.Content == "cron-response");

        supervisor.Verify(
            s => s.GetOrCreateAsync(AgentId.From("agent-a"), sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
