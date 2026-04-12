using System.Reflection;
using BotNexus.Domain.Primitives;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Abstractions.Sessions;
using BotNexus.Gateway.Agents;
using BotNexus.Gateway.Sessions;
using FluentAssertions;

namespace BotNexus.Gateway.Tests.Sessions;

public sealed class SessionModelWave3TypedIdTests
{
    [Fact]
    public void GatewaySession_UsesTypedAgentIdAndSessionId()
    {
        typeof(GatewaySession).GetProperty(nameof(GatewaySession.AgentId))!.PropertyType.Should().Be(typeof(AgentId));
        typeof(GatewaySession).GetProperty(nameof(GatewaySession.SessionId))!.PropertyType.Should().Be(typeof(SessionId));
    }

    [Fact]
    public void DefaultAgentSupervisor_UsesAgentSessionKey_NotLegacyMakeKey()
    {
        var makeKeyMethod = typeof(DefaultAgentSupervisor).GetMethod("MakeKey", BindingFlags.NonPublic | BindingFlags.Static);
        makeKeyMethod.Should().BeNull();

        var keyBackedFields = typeof(DefaultAgentSupervisor)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => field.FieldType.IsGenericType)
            .Select(field => field.FieldType.GetGenericArguments()[0])
            .ToArray();

        keyBackedFields.Should().Contain(typeof(AgentSessionKey));
    }

    [Fact]
    public async Task InMemorySessionStore_SupportsTypedIds_WithStringBackCompat()
    {
        var store = new InMemorySessionStore();
        var sessionId = SessionId.From("typed-session");
        var agentId = AgentId.From("typed-agent");

        var session = await store.GetOrCreateAsync(sessionId, agentId);
        session.ChannelType = ChannelKey.From("signalr");
        await store.SaveAsync(session);

        var loaded = await store.GetAsync(sessionId);
        var listed = await store.ListAsync(agentId);
        var byChannel = await store.ListByChannelAsync(agentId, ChannelKey.From("signalr"));

        loaded.Should().NotBeNull();
        loaded!.SessionId.Should().Be(sessionId);
        loaded.AgentId.Should().Be(agentId);
        listed.Should().ContainSingle();
        byChannel.Should().ContainSingle();

        string sessionIdString = loaded.SessionId;
        string agentIdString = loaded.AgentId;
        sessionIdString.Should().Be("typed-session");
        agentIdString.Should().Be("typed-agent");
    }

    [Fact]
    public void SessionStoreInterface_UsesTypedIdParameters()
    {
        var getOrCreate = typeof(ISessionStore).GetMethod(nameof(ISessionStore.GetOrCreateAsync));
        getOrCreate.Should().NotBeNull();
        getOrCreate!.GetParameters()[0].ParameterType.Should().Be(typeof(SessionId));
        getOrCreate.GetParameters()[1].ParameterType.Should().Be(typeof(AgentId));
    }
}
