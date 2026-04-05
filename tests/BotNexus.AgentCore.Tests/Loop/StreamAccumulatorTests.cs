using BotNexus.AgentCore.Loop;
using BotNexus.AgentCore.Types;
using BotNexus.Providers.Core.Models;
using BotNexus.Providers.Core.Streaming;
using FluentAssertions;

namespace BotNexus.AgentCore.Tests.Loop;

public class StreamAccumulatorTests
{
    [Fact]
    public async Task AccumulateAsync_StreamWithoutStartEvent_EmitsMessageStartThenMessageEnd()
    {
        var stream = new LlmStream();
        var completion = new AssistantMessage(
            Content: [new TextContent("done")],
            Api: "test-api",
            Provider: "test-provider",
            ModelId: "test-model",
            Usage: Usage.Empty(),
            StopReason: StopReason.Stop,
            ErrorMessage: null,
            ResponseId: "resp_1",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var eventTypes = new List<AgentEventType>();

        stream.Push(new DoneEvent(StopReason.Stop, completion));
        stream.End(completion);

        _ = await StreamAccumulator.AccumulateAsync(
            stream,
            evt =>
            {
                eventTypes.Add(evt.Type);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        eventTypes.Should().Equal(AgentEventType.MessageStart, AgentEventType.MessageEnd);
    }

    [Fact]
    public async Task AccumulateAsync_ErrorEventPreservesOriginalStopReason()
    {
        var stream = new LlmStream();
        var errorMessage = new AssistantMessage(
            Content: [new TextContent("aborted")],
            Api: "test-api",
            Provider: "test-provider",
            ModelId: "test-model",
            Usage: Usage.Empty(),
            StopReason: StopReason.Aborted,
            ErrorMessage: "aborted",
            ResponseId: "resp_err",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        stream.Push(new ErrorEvent(StopReason.Aborted, errorMessage));
        stream.End(errorMessage);

        var result = await StreamAccumulator.AccumulateAsync(stream, _ => Task.CompletedTask, CancellationToken.None);
        result.FinishReason.Should().Be(StopReason.Aborted);
    }

    [Fact]
    public async Task AccumulateAsync_UpdatesContextMessagesWithStreamingPartial()
    {
        var stream = new LlmStream();
        var start = CreateAssistantMessage("h");
        var partial = CreateAssistantMessage("hello");
        var final = CreateAssistantMessage("hello world");
        var contextMessages = new List<AgentMessage> { new BotNexus.AgentCore.Types.UserMessage("prompt") };

        stream.Push(new StartEvent(start));
        stream.Push(new TextDeltaEvent(0, "ello", partial));
        stream.Push(new DoneEvent(StopReason.Stop, final));
        stream.End(final);

        _ = await StreamAccumulator.AccumulateAsync(
            stream,
            _ => Task.CompletedTask,
            CancellationToken.None,
            contextMessages);

        contextMessages.Should().HaveCount(2);
        contextMessages[^1].Should().BeOfType<AssistantAgentMessage>()
            .Which.Content.Should().Be("hello world");
    }

    private static AssistantMessage CreateAssistantMessage(string content)
    {
        return new AssistantMessage(
            Content: [new TextContent(content)],
            Api: "test-api",
            Provider: "test-provider",
            ModelId: "test-model",
            Usage: Usage.Empty(),
            StopReason: StopReason.Stop,
            ErrorMessage: null,
            ResponseId: "resp",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
}
