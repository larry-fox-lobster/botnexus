using BotNexus.Gateway.Abstractions.Media;
using BotNexus.Gateway.Abstractions.Models;
using FluentAssertions;

namespace BotNexus.Gateway.Tests.Media;

public sealed class MediaContractTests
{
    [Fact]
    public void MediaProcessingContext_Constructor_WithRequiredProperties_ShouldPreserveValues()
    {
        var context = new MediaProcessingContext
        {
            SessionId = "session-1",
            ChannelType = "signalr"
        };

        context.SessionId.Should().Be("session-1");
        context.ChannelType.Should().Be("signalr");
        context.CancellationToken.Should().Be(CancellationToken.None);
    }

    [Fact]
    public void MediaProcessingResult_Constructor_WithRequiredProperties_ShouldPreserveValuesAndDefaults()
    {
        var processedPart = new TextContentPart
        {
            MimeType = "text/plain",
            Text = "processed"
        };
        var result = new MediaProcessingResult
        {
            ProcessedPart = processedPart
        };

        result.ProcessedPart.Should().BeSameAs(processedPart);
        result.WasTransformed.Should().BeFalse();
        result.Metadata.Should().BeNull();
    }
}
