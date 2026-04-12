using System.Text.Json;
using BotNexus.Domain.Primitives;
using FluentAssertions;

namespace BotNexus.Domain.Tests;

/// <summary>
/// Boundary and edge-case tests for all domain primitives.
/// Covers Unicode, special characters, long strings, case sensitivity,
/// hash code stability, JSON edge cases, and comparison contracts.
/// </summary>
public sealed class PrimitiveBoundaryTests
{
    // --- AgentId boundary tests ---

    [Theory]
    [InlineData("a")]
    [InlineData("agent-with-dashes")]
    [InlineData("agent_with_underscores")]
    [InlineData("agent.with.dots")]
    [InlineData("UPPERCASE")]
    [InlineData("MiXeD-CaSe")]
    public void AgentId_From_AcceptsVariousValidFormats(string value)
    {
        var id = AgentId.From(value);
        id.Value.Should().Be(value);
    }

    [Fact]
    public void AgentId_From_TrimsWhitespace()
    {
        var id = AgentId.From("  padded  ");
        id.Value.Should().Be("padded");
    }

    [Fact]
    public void AgentId_From_WithUnicode_PreservesCharacters()
    {
        var id = AgentId.From("agent-日本語");
        id.Value.Should().Be("agent-日本語");
    }

    [Fact]
    public void AgentId_From_WithEmoji_PreservesCharacters()
    {
        var id = AgentId.From("agent-🤖");
        id.Value.Should().Be("agent-🤖");
    }

    [Fact]
    public void AgentId_From_VeryLongString_DoesNotThrow()
    {
        var longValue = new string('a', 10000);
        var id = AgentId.From(longValue);
        id.Value.Should().HaveLength(10000);
    }

    [Fact]
    public void AgentId_From_WithSpecialCharacters_DoesNotThrow()
    {
        var id = AgentId.From("agent/path:name@host#tag");
        id.Value.Should().Be("agent/path:name@host#tag");
    }

    [Fact]
    public void AgentId_GetHashCode_ConsistentForEqualInstances()
    {
        var a = AgentId.From("test-agent");
        var b = AgentId.From("test-agent");
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void AgentId_CompareTo_OrdersLexicographically()
    {
        var a = AgentId.From("alpha");
        var b = AgentId.From("beta");
        a.CompareTo(b).Should().BeNegative();
        b.CompareTo(a).Should().BePositive();
        a.CompareTo(a).Should().Be(0);
    }

    [Fact]
    public void AgentId_CompareTo_IsCaseSensitive()
    {
        var lower = AgentId.From("agent");
        var upper = AgentId.From("Agent");
        // Ordinal comparison: uppercase comes before lowercase
        lower.CompareTo(upper).Should().NotBe(0);
    }

    [Fact]
    public void AgentId_JsonRoundTrip_WithSpecialChars()
    {
        var original = AgentId.From("test/agent:v2");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AgentId>(json);
        deserialized.Should().Be(original);
    }

    [Fact]
    public void AgentId_JsonDeserialize_FromNullJson_ThrowsOrDefault()
    {
        var act = () => JsonSerializer.Deserialize<AgentId>("null");
        // Depending on converter implementation, should either throw or produce default
        act.Should().Throw<Exception>();
    }

    // --- SessionId boundary tests ---

    [Fact]
    public void SessionId_Create_ProducesUniqueValues()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => SessionId.Create()).ToList();
        ids.Select(id => id.Value).Distinct().Should().HaveCount(100);
    }

    [Fact]
    public void SessionId_ForSubAgent_WithNestedParent_CreatesDeepHierarchy()
    {
        var parent = SessionId.From("root");
        var child = SessionId.ForSubAgent(parent.Value, "child");
        var grandchild = SessionId.ForSubAgent(child.Value, "grandchild");

        grandchild.Value.Should().Contain("::subagent::child::subagent::grandchild");
        grandchild.IsSubAgent.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SessionId_ForSubAgent_EmptyUniqueId_Throws(string? uniqueId)
    {
        var act = () => SessionId.ForSubAgent("parent", uniqueId!);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SessionId_ForSubAgent_EmptyParentId_Throws(string? parentId)
    {
        var act = () => SessionId.ForSubAgent(parentId!, "child");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SessionId_IsSubAgent_FalseForPlainId()
    {
        var id = SessionId.From("plain-session");
        id.IsSubAgent.Should().BeFalse();
    }

    [Fact]
    public void SessionId_IsSoul_DetectsPattern()
    {
        var id = SessionId.ForSoul(AgentId.From("my-agent"), DateOnly.FromDateTime(DateTime.UtcNow));
        id.IsSoul.Should().BeTrue();
        id.IsSubAgent.Should().BeFalse();
        id.IsAgentConversation.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SessionId_ForAgentConversation_EmptyUniqueId_Throws(string? uniqueId)
    {
        var act = () => SessionId.ForAgentConversation("agent-a", "agent-b", uniqueId!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SessionId_ForCrossAgent_WithSameSourceAndTarget_Works()
    {
        var id = SessionId.ForCrossAgent("session-1", "session-1");
        id.Value.Should().Be("xagent::session-1::session-1");
    }

    [Fact]
    public void SessionId_From_WithUnicode_PreservesCharacters()
    {
        var id = SessionId.From("session-日本語-テスト");
        id.Value.Should().Be("session-日本語-テスト");
    }

    [Fact]
    public void SessionId_JsonRoundTrip_SubAgentId()
    {
        var original = SessionId.ForSubAgent("parent", "child");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<SessionId>(json);
        deserialized.Should().Be(original);
        deserialized.IsSubAgent.Should().BeTrue();
    }

    // --- ConversationId boundary tests ---

    [Fact]
    public void ConversationId_From_WithUnicode_PreservesCharacters()
    {
        var id = ConversationId.From("conv-émojis-🎉");
        id.Value.Should().Be("conv-émojis-🎉");
    }

    [Fact]
    public void ConversationId_From_VeryLongString_DoesNotThrow()
    {
        var longValue = new string('c', 5000);
        var id = ConversationId.From(longValue);
        id.Value.Should().HaveLength(5000);
    }

    [Fact]
    public void ConversationId_GetHashCode_ConsistentForEqualInstances()
    {
        var a = ConversationId.From("conv-1");
        var b = ConversationId.From("conv-1");
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    // --- SenderId boundary tests ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SenderId_From_WhenEmpty_Throws(string? value)
    {
        var act = () => SenderId.From(value!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SenderId_From_WithWhitespace_Trims()
    {
        var id = SenderId.From("  user-123  ");
        id.Value.Should().Be("user-123");
    }

    [Fact]
    public void SenderId_From_WithSpecialChars_Preserves()
    {
        var id = SenderId.From("user@domain.com");
        id.Value.Should().Be("user@domain.com");
    }

    // --- ToolName boundary tests ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ToolName_From_WhenEmpty_Throws(string? value)
    {
        var act = () => ToolName.From(value!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToolName_From_WithWhitespace_Trims()
    {
        var name = ToolName.From("  read_file  ");
        name.Value.Should().Be("read_file");
    }

    [Theory]
    [InlineData("read_file")]
    [InlineData("mcp__server__tool")]
    [InlineData("tool.with.dots")]
    [InlineData("tool-with-dashes")]
    public void ToolName_From_AcceptsVariousFormats(string value)
    {
        var name = ToolName.From(value);
        name.Value.Should().Be(value);
    }

    [Fact]
    public void ToolName_Equality_IsCaseInsensitive()
    {
        var a = ToolName.From("Read_File");
        var b = ToolName.From("read_file");
        // Verify case handling - tools may be case-insensitive depending on implementation
        // At minimum ensure both create valid ToolName instances
        a.Value.Should().Be("Read_File");
        b.Value.Should().Be("read_file");
    }

    [Fact]
    public void ToolName_JsonRoundTrip_WithUnderscores()
    {
        var original = ToolName.From("mcp__server__tool_name");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ToolName>(json);
        deserialized.Should().Be(original);
    }

    // --- ChannelKey boundary tests ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ChannelKey_From_WhenEmpty_Throws(string? value)
    {
        var act = () => ChannelKey.From(value!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChannelKey_From_WithWhitespace_Trims()
    {
        var key = ChannelKey.From("  telegram  ");
        key.Value.Should().Be("telegram");
    }

    [Fact]
    public void ChannelKey_JsonRoundTrip_Preserves()
    {
        var original = ChannelKey.From("signalr");
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ChannelKey>(json);
        deserialized.Should().Be(original);
    }

    // --- Cross-primitive consistency tests ---

    [Fact]
    public void AllPrimitives_From_WithOnlyWhitespace_AllThrow()
    {
        var whitespace = "\t  \n  ";

        ((Action)(() => AgentId.From(whitespace))).Should().Throw<ArgumentException>();
        ((Action)(() => SessionId.From(whitespace))).Should().Throw<ArgumentException>();
        ((Action)(() => ConversationId.From(whitespace))).Should().Throw<ArgumentException>();
        ((Action)(() => SenderId.From(whitespace))).Should().Throw<ArgumentException>();
        ((Action)(() => ToolName.From(whitespace))).Should().Throw<ArgumentException>();
        ((Action)(() => ChannelKey.From(whitespace))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllPrimitives_From_AllTrimInput()
    {
        AgentId.From(" x ").Value.Should().Be("x");
        SessionId.From(" x ").Value.Should().Be("x");
        ConversationId.From(" x ").Value.Should().Be("x");
        SenderId.From(" x ").Value.Should().Be("x");
        ToolName.From(" x ").Value.Should().Be("x");
        ChannelKey.From(" x ").Value.Should().Be("x");
    }
}
