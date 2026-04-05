using System.Reflection;
using BotNexus.AgentCore.Configuration;
using BotNexus.AgentCore.Types;
using BotNexus.Providers.Core.Models;
using FluentAssertions;
using ProviderUserMessage = BotNexus.Providers.Core.Models.UserMessage;

namespace BotNexus.CodingAgent.Tests;

public sealed class CodingAgentTests
{
    [Fact]
    public async Task BuildConvertToLlmDelegate_MapsSystemMessageToUserMessage()
    {
        var method = typeof(CodingAgent).GetMethod("BuildConvertToLlmDelegate", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        var convertToLlm = method!.Invoke(null, null).Should().BeAssignableTo<ConvertToLlmDelegate>().Subject;

        var providerMessages = await convertToLlm([new SystemAgentMessage("[Session context summary: compacted]")], CancellationToken.None);

        providerMessages.Should().ContainSingle();
        providerMessages[0].Should().BeOfType<ProviderUserMessage>();
        providerMessages[0].As<ProviderUserMessage>().Content.Text.Should().Contain("Session context summary");
    }
}
