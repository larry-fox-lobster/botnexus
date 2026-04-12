using System.Reflection;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Abstractions.Sessions;
using BotNexus.Gateway.Sessions;
using FluentAssertions;

namespace BotNexus.Gateway.Tests.Sessions;

public sealed class ListByChannelTests
{
    [Fact]
    public async Task FiltersByAgentAndChannel()
    {
        var store = new InMemorySessionStore();
        await SaveSessionAsync(store, "match", "agent-a", "web", DateTimeOffset.UtcNow.AddMinutes(-3));
        await SaveSessionAsync(store, "wrong-agent", "agent-b", "web", DateTimeOffset.UtcNow.AddMinutes(-2));
        await SaveSessionAsync(store, "wrong-channel", "agent-a", "telegram", DateTimeOffset.UtcNow.AddMinutes(-1));

        var sessions = await InvokeListByChannelAsync(store, "agent-a", "web");

        sessions.Select(s => s.SessionId).Should().BeEquivalentTo(["match"]);
    }

    [Fact]
    public async Task OrderedByCreatedAtDescending()
    {
        var store = new InMemorySessionStore();
        await SaveSessionAsync(store, "oldest", "agent-a", "web", DateTimeOffset.UtcNow.AddMinutes(-30));
        await SaveSessionAsync(store, "middle", "agent-a", "web", DateTimeOffset.UtcNow.AddMinutes(-20));
        await SaveSessionAsync(store, "newest", "agent-a", "web", DateTimeOffset.UtcNow.AddMinutes(-10));

        var sessions = await InvokeListByChannelAsync(store, "agent-a", "web");

        sessions.Select(s => s.SessionId).Should().ContainInOrder("newest", "middle", "oldest");
        sessions.Select(s => s.CreatedAt).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task SkipsNullChannelType()
    {
        var store = new InMemorySessionStore();
        await SaveSessionAsync(store, "null-channel", "agent-a", null, DateTimeOffset.UtcNow.AddMinutes(-2));
        await SaveSessionAsync(store, "web", "agent-a", "web", DateTimeOffset.UtcNow.AddMinutes(-1));

        var sessions = await InvokeListByChannelAsync(store, "agent-a", "web");

        sessions.Select(s => s.SessionId).Should().BeEquivalentTo(["web"]);
    }

    private static async Task SaveSessionAsync(
        ISessionStore store,
        string sessionId,
        string agentId,
        string? channelType,
        DateTimeOffset createdAt)
    {
        var session = new GatewaySession
        {
            SessionId = sessionId,
            AgentId = agentId,
            ChannelType = channelType,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        await store.SaveAsync(session, CancellationToken.None);
    }

    private static async Task<IReadOnlyList<GatewaySession>> InvokeListByChannelAsync(
        ISessionStore store,
        string agentId,
        string channelType)
    {
        var method = store.GetType().GetMethod(
            "ListByChannelAsync",
            BindingFlags.Instance | BindingFlags.Public,
            [typeof(string), typeof(string), typeof(CancellationToken)]);

        method.Should().NotBeNull("ListByChannelAsync must exist on session store implementations.");
        var invocationResult = method!.Invoke(store, [agentId, channelType, CancellationToken.None]);
        invocationResult.Should().BeAssignableTo<Task>();

        var task = (Task)invocationResult!;
        await task;

        var resultProperty = task.GetType().GetProperty("Result");
        resultProperty.Should().NotBeNull();
        var sessions = resultProperty!.GetValue(task) as IReadOnlyList<GatewaySession>;
        sessions.Should().NotBeNull();
        return sessions!;
    }
}
