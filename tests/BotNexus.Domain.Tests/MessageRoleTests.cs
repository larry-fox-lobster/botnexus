using System.Text.Json;
using BotNexus.Domain.Primitives;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class MessageRoleTests
{
    [Fact]
    public void MessageRole_KnownValues_WhenAccessed_ShouldExist()
    {
        MessageRole.User.Value.Should().Be("user");
    }

    [Fact]
    public void MessageRole_FromString_WhenValueIsKnown_ShouldReturnKnownInstance()
    {
        var role = MessageRole.FromString("USER");
        role.Should().BeSameAs(MessageRole.User);
    }

    [Fact]
    public void MessageRole_FromString_WhenValueIsUnknown_ShouldCreateExtensibleInstance()
    {
        var role = MessageRole.FromString("custom-role");
        role.Value.Should().Be("custom-role");
    }

    [Fact]
    public void MessageRole_FromString_WhenValueHasDifferentCase_ShouldMatchCaseInsensitively()
    {
        var first = MessageRole.FromString("CUSTOM-ROLE");
        var second = MessageRole.FromString("custom-role");
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void MessageRole_ImplicitConversion_WhenConvertedToString_ShouldReturnValue()
    {
        string value = MessageRole.Assistant;
        value.Should().Be("assistant");
    }

    [Fact]
    public void MessageRole_Equals_WhenValuesMatch_ShouldBeTrue()
    {
        var left = MessageRole.FromString("tool");
        var right = MessageRole.Tool;
        left.Should().Be(right);
    }

    [Fact]
    public void MessageRole_Equals_WhenValuesDiffer_ShouldBeFalse()
    {
        var left = MessageRole.User;
        var right = MessageRole.Assistant;
        left.Should().NotBe(right);
    }

    [Fact]
    public void MessageRole_JsonRoundTrip_WhenSerializedAndDeserialized_ShouldBeEqual()
    {
        var roundTrip = JsonSerializer.Deserialize<MessageRole>(JsonSerializer.Serialize(MessageRole.System));
        roundTrip.Should().Be(MessageRole.System);
    }

    [Fact]
    public async Task MessageRole_FromString_WhenCalledConcurrently_ShouldBeThreadSafe()
    {
        var tasks = Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() => MessageRole.FromString("thread-role")))
            .ToArray();
        var results = await Task.WhenAll(tasks);
        results.Distinct().Should().HaveCount(1);
    }
}
