using System.Text.Json;
using BotNexus.Domain.Primitives;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

public sealed class ToolNameTests
{
    [Fact]
    public void ToolName_From_WhenValueIsValid_ShouldCreateInstance()
    {
        var result = ToolName.From(" tool.exec ");
        result.Value.Should().Be("tool.exec");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ToolName_From_WhenValueIsEmpty_ShouldThrowArgumentException(string? value)
    {
        var action = () => ToolName.From(value!);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToolName_Equals_WhenValuesMatchCaseInsensitively_ShouldBeTrue()
    {
        var left = ToolName.From("TOOL.EXEC");
        var right = ToolName.From("tool.exec");
        left.Should().Be(right);
    }

    [Fact]
    public void ToolName_Equals_WhenValuesDiffer_ShouldBeFalse()
    {
        var left = ToolName.From("tool.exec");
        var right = ToolName.From("tool.search");
        left.Should().NotBe(right);
    }

    [Fact]
    public void ToolName_ImplicitConversion_WhenConvertedToString_ShouldReturnValue()
    {
        var toolName = ToolName.From("tool.exec");
        string value = toolName;
        value.Should().Be("tool.exec");
    }

    [Fact]
    public void ToolName_ExplicitConversion_WhenConvertedFromString_ShouldCreateInstance()
    {
        var toolName = (ToolName)"tool.exec";
        toolName.Value.Should().Be("tool.exec");
    }

    [Fact]
    public void ToolName_ToString_WhenCalled_ShouldReturnValue()
    {
        var toolName = ToolName.From("tool.exec");
        toolName.ToString().Should().Be("tool.exec");
    }

    [Fact]
    public void ToolName_JsonRoundTrip_WhenSerializedAndDeserialized_ShouldBeEqual()
    {
        var original = ToolName.From("tool.exec");
        var roundTrip = JsonSerializer.Deserialize<ToolName>(JsonSerializer.Serialize(original));
        roundTrip.Should().Be(original);
    }
}
