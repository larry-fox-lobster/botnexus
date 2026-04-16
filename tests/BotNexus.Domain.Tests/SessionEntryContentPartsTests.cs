using BotNexus.Domain.Primitives;
using BotNexus.Gateway.Abstractions.Models;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class SessionEntryContentPartsTests
{
    [Fact]
    public void SessionEntry_Constructor_WithoutContentPartLists_ShouldDefaultToNull()
    {
        var entry = CreateEntry();

        entry.OriginalContentParts.Should().BeNull();
        entry.ProcessedContentParts.Should().BeNull();
    }

    [Fact]
    public void SessionEntry_Constructor_WithBothContentPartLists_ShouldPreserveValues()
    {
        var original = new MessageContentPart[]
        {
            new BinaryContentPart { MimeType = "audio/wav", Data = [1, 2, 3] }
        };
        var processed = new MessageContentPart[]
        {
            new TextContentPart { MimeType = "text/plain", Text = "transcribed text" }
        };
        var entry = CreateEntry() with
        {
            OriginalContentParts = original,
            ProcessedContentParts = processed
        };

        entry.OriginalContentParts.Should().BeEquivalentTo(original);
        entry.ProcessedContentParts.Should().BeEquivalentTo(processed);
    }

    [Fact]
    public void SessionEntry_Constructor_WithOriginalContentPartsOnly_ShouldSupportPreProcessingState()
    {
        var original = new MessageContentPart[]
        {
            new ReferenceContentPart { MimeType = "audio/mpeg", Uri = "https://example.invalid/audio.mp3" }
        };
        var entry = CreateEntry() with { OriginalContentParts = original };

        entry.OriginalContentParts.Should().BeEquivalentTo(original);
        entry.ProcessedContentParts.Should().BeNull();
    }

    [Fact]
    public void SessionEntry_ContentPartLists_ShouldRemainIndependent()
    {
        var original = new MessageContentPart[]
        {
            new BinaryContentPart { MimeType = "audio/wav", Data = [1, 2, 3], FileName = "input.wav" }
        };
        var processed = new MessageContentPart[]
        {
            new TextContentPart { MimeType = "text/plain", Text = "hello world" }
        };
        var entry = CreateEntry() with
        {
            OriginalContentParts = original,
            ProcessedContentParts = processed
        };

        entry.OriginalContentParts.Should().HaveCount(1);
        entry.ProcessedContentParts.Should().HaveCount(1);
        entry.OriginalContentParts![0].Should().NotBeSameAs(entry.ProcessedContentParts![0]);
        entry.OriginalContentParts[0].Should().BeOfType<BinaryContentPart>();
        entry.ProcessedContentParts[0].Should().BeOfType<TextContentPart>();
    }

    private static SessionEntry CreateEntry() => new()
    {
        Role = MessageRole.User,
        Content = "hello"
    };
}
