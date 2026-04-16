using BotNexus.Gateway.Abstractions.Models;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class MessageContentPartTests
{
    [Fact]
    public void TextContentPart_Constructor_WithRequiredProperties_ShouldPreserveValues()
    {
        var part = new TextContentPart
        {
            MimeType = "text/plain",
            Text = "hello"
        };

        part.MimeType.Should().Be("text/plain");
        part.Text.Should().Be("hello");
    }

    [Fact]
    public void TextContentPart_RecordEquality_WithSameValues_ShouldBeEqual()
    {
        var left = new TextContentPart { MimeType = "text/plain", Text = "hello" };
        var right = new TextContentPart { MimeType = "text/plain", Text = "hello" };

        left.Should().Be(right);
    }

    [Fact]
    public void TextContentPart_WithExpression_ShouldCreateModifiedCopy()
    {
        var original = new TextContentPart { MimeType = "text/plain", Text = "hello" };

        var updated = original with { Text = "updated" };

        updated.MimeType.Should().Be("text/plain");
        updated.Text.Should().Be("updated");
        original.Text.Should().Be("hello");
    }

    [Fact]
    public void BinaryContentPart_Constructor_WithRequiredProperties_ShouldPreserveValues()
    {
        var data = new byte[] { 1, 2, 3 };
        var part = new BinaryContentPart
        {
            MimeType = "audio/wav",
            Data = data
        };

        part.MimeType.Should().Be("audio/wav");
        part.Data.Should().BeSameAs(data);
        part.FileName.Should().BeNull();
    }

    [Fact]
    public void BinaryContentPart_RecordEquality_WithSameArrayReference_ShouldBeEqual()
    {
        var shared = new byte[] { 1, 2, 3 };
        var left = new BinaryContentPart { MimeType = "application/octet-stream", Data = shared };
        var right = new BinaryContentPart { MimeType = "application/octet-stream", Data = shared };

        left.Should().Be(right);
    }

    [Fact]
    public void BinaryContentPart_RecordEquality_WithDifferentArrayInstancesSameValues_ShouldNotBeEqual()
    {
        var left = new BinaryContentPart { MimeType = "application/octet-stream", Data = [1, 2, 3] };
        var right = new BinaryContentPart { MimeType = "application/octet-stream", Data = [1, 2, 3] };

        left.Should().NotBe(right);
    }

    [Fact]
    public void ReferenceContentPart_Constructor_WithRequiredProperties_ShouldPreserveValues()
    {
        var part = new ReferenceContentPart
        {
            MimeType = "image/png",
            Uri = "https://example.invalid/image.png"
        };

        part.MimeType.Should().Be("image/png");
        part.Uri.Should().Be("https://example.invalid/image.png");
        part.SizeBytes.Should().BeNull();
        part.FileName.Should().BeNull();
    }

    [Fact]
    public void ReferenceContentPart_RecordEquality_WithSameValues_ShouldBeEqual()
    {
        var left = new ReferenceContentPart
        {
            MimeType = "image/png",
            Uri = "https://example.invalid/image.png",
            SizeBytes = 123,
            FileName = "image.png"
        };
        var right = new ReferenceContentPart
        {
            MimeType = "image/png",
            Uri = "https://example.invalid/image.png",
            SizeBytes = 123,
            FileName = "image.png"
        };

        left.Should().Be(right);
    }

    [Fact]
    public void MessageContentPart_Polymorphism_ShouldSupportAllDerivedTypesInSingleList()
    {
        MessageContentPart text = new TextContentPart { MimeType = "text/plain", Text = "hello" };
        MessageContentPart binary = new BinaryContentPart { MimeType = "application/octet-stream", Data = [1] };
        MessageContentPart reference = new ReferenceContentPart { MimeType = "image/png", Uri = "https://example.invalid/x.png" };

        IReadOnlyList<MessageContentPart> parts = [text, binary, reference];

        text.Should().BeAssignableTo<MessageContentPart>();
        binary.Should().BeAssignableTo<MessageContentPart>();
        reference.Should().BeAssignableTo<MessageContentPart>();
        parts.Should().HaveCount(3);
        parts[0].Should().BeOfType<TextContentPart>();
        parts[1].Should().BeOfType<BinaryContentPart>();
        parts[2].Should().BeOfType<ReferenceContentPart>();
    }
}
