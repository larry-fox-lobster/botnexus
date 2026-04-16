using System.Runtime.CompilerServices;
using BotNexus.Domain.Primitives;
using BotNexus.Gateway.Abstractions.Models;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class InboundMessageContentPartsTests
{
    [Fact]
    public void InboundMessage_Constructor_WithoutContentParts_ShouldDefaultToNull()
    {
        var message = CreateMessage();

        message.ContentParts.Should().BeNull();
    }

    [Fact]
    public void InboundMessage_Constructor_WithEmptyContentParts_ShouldSucceed()
    {
        var message = CreateMessage() with { ContentParts = [] };

        message.ContentParts.Should().NotBeNull();
        message.ContentParts.Should().BeEmpty();
    }

    [Fact]
    public void InboundMessage_Constructor_WithMixedContentParts_ShouldPreserveValues()
    {
        var text = new TextContentPart { MimeType = "text/plain", Text = "hello" };
        var binary = new BinaryContentPart { MimeType = "application/octet-stream", Data = [1, 2] };
        var reference = new ReferenceContentPart { MimeType = "image/png", Uri = "https://example.invalid/image.png" };
        var message = CreateMessage() with { ContentParts = [text, binary, reference] };

        message.ContentParts.Should().NotBeNull();
        message.ContentParts.Should().HaveCount(3);
        message.ContentParts![0].Should().BeSameAs(text);
        message.ContentParts[1].Should().BeSameAs(binary);
        message.ContentParts[2].Should().BeSameAs(reference);
    }

    [Fact]
    public void InboundMessage_ContentProperty_ShouldBeRequiredEvenWhenContentPartsIsSet()
    {
        var contentProperty = typeof(InboundMessage).GetProperty(nameof(InboundMessage.Content));

        contentProperty.Should().NotBeNull();
        contentProperty!.GetCustomAttributes(typeof(RequiredMemberAttribute), inherit: false)
            .Should()
            .HaveCount(1);
    }

    [Fact]
    public void InboundMessage_ContentParts_WhenSpecified_ShouldPreserveOrder()
    {
        var first = new TextContentPart { MimeType = "text/plain", Text = "first" };
        var second = new TextContentPart { MimeType = "text/plain", Text = "second" };
        var third = new TextContentPart { MimeType = "text/plain", Text = "third" };
        var message = CreateMessage() with { ContentParts = [first, second, third] };

        message.ContentParts.Should().NotBeNull();
        message.ContentParts!.Should().ContainInOrder(first, second, third);
    }

    private static InboundMessage CreateMessage() => new()
    {
        ChannelType = ChannelKey.From("signalr"),
        SenderId = "sender-1",
        ConversationId = "conversation-1",
        Content = "hello"
    };
}
