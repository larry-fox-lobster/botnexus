using BotNexus.AgentCore.Configuration;
using BotNexus.AgentCore.Types;
using FluentAssertions;
using ProviderUserMessage = BotNexus.Providers.Core.Models.UserMessage;

namespace BotNexus.CodingAgent.Tests;

public sealed class CodingAgentTests
{
    [Fact]
    public async Task DefaultMessageConverter_MapsSystemMessageToUserMessage()
    {
        var convertToLlm = DefaultMessageConverter.Create();

        var providerMessages = await convertToLlm([new SystemAgentMessage("[Session context summary: compacted]")], CancellationToken.None);

        providerMessages.Should().ContainSingle();
        providerMessages[0].Should().BeOfType<ProviderUserMessage>();
        providerMessages[0].As<ProviderUserMessage>().Content.Text.Should().Contain("Session context summary");
    }
}
