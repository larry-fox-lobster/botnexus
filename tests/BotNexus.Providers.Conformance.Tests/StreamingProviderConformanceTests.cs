using System.Net;
using System.Text;
using BotNexus.Providers.Core;
using BotNexus.Providers.Core.Models;
using BotNexus.Providers.Core.Registry;
using BotNexus.Providers.Core.Streaming;
using FluentAssertions;

namespace BotNexus.Providers.Conformance.Tests;

public abstract class StreamingProviderConformanceTests
{
    public static TheoryData<string> TextCases => new()
    {
        "normalized hello",
        "multiline\ncontent"
    };

    public static TheoryData<string, string, string> ToolCallCases => new()
    {
        { "call_1", "search", "{\"query\":\"weather\"}" },
        { "call_2", "lookup", "{\"id\":\"42\"}" }
    };

    public static TheoryData<int, int> UsageCases => new()
    {
        { 11, 5 },
        { 100, 25 }
    };

    public static TheoryData<string, StopReason> StopReasonCases => new()
    {
        { "stop", StopReason.Stop },
        { "length", StopReason.Length },
        { "tool_use", StopReason.ToolUse }
    };

    [Theory]
    [MemberData(nameof(TextCases))]
    public async Task Stream_NormalizesContentExtraction(string expectedText)
    {
        var (result, _) = await ExecuteAsync(BuildTextPayload(expectedText, MapCanonicalStopReason("stop")));

        result.Content.Should().ContainSingle();
        result.Content[0].Should().BeOfType<TextContent>();
        ((TextContent)result.Content[0]).Text.Should().Be(expectedText);
    }

    [Theory]
    [MemberData(nameof(ToolCallCases))]
    public async Task Stream_NormalizesToolCallParsing(string toolCallId, string toolName, string argumentsJson)
    {
        var (result, _) = await ExecuteAsync(
            BuildToolCallPayload(toolCallId, toolName, argumentsJson, MapCanonicalStopReason("tool_use")));

        var toolCall = result.Content.OfType<ToolCallContent>().Single();
        toolCall.Id.Should().Be(toolCallId);
        toolCall.Name.Should().Be(toolName);
        toolCall.Arguments.Keys.Any(key => key == "query" || key == "id").Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(StopReasonCases))]
    public async Task Stream_NormalizesFinishReasons(string canonicalReason, StopReason expected)
    {
        var (result, _) = await ExecuteAsync(BuildFinishReasonPayload(MapCanonicalStopReason(canonicalReason)));

        result.StopReason.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(UsageCases))]
    public async Task Stream_NormalizesTokenCounts(int inputTokens, int outputTokens)
    {
        var (result, _) = await ExecuteAsync(
            BuildUsagePayload(inputTokens, outputTokens, MapCanonicalStopReason("stop")));

        result.Usage.Input.Should().Be(inputTokens);
        result.Usage.Output.Should().Be(outputTokens);
        result.Usage.TotalTokens.Should().Be(inputTokens + outputTokens);
    }

    [Theory]
    [MemberData(nameof(TextCases))]
    public async Task Stream_EmitsExpectedEventSequence(string text)
    {
        if (!SupportsStreamingSequence)
            return;

        var (_, events) = await ExecuteAsync(BuildTextPayload(text, MapCanonicalStopReason("stop")));

        events.Select(e => e.Type).Should().Equal(ExpectedTextEventSequence);
    }

    protected virtual bool SupportsStreamingSequence => true;

    protected virtual IReadOnlyList<string> ExpectedTextEventSequence =>
        ["start", "text_start", "text_delta", "text_end", "done"];

    protected virtual Context CreateContext() => new(
        SystemPrompt: "You are helpful",
        Messages: [new UserMessage(new UserMessageContent("hello"), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())]);

    protected virtual StreamOptions CreateOptions() => new() { ApiKey = "test-key" };

    protected abstract IApiProvider CreateProvider(HttpMessageHandler handler);
    protected abstract LlmModel CreateModel();
    protected abstract string BuildTextPayload(string text, string providerStopReason);
    protected abstract string BuildToolCallPayload(string toolCallId, string toolName, string argumentsJson, string providerStopReason);
    protected abstract string BuildFinishReasonPayload(string providerStopReason);
    protected abstract string BuildUsagePayload(int inputTokens, int outputTokens, string providerStopReason);
    protected abstract string MapCanonicalStopReason(string canonicalReason);

    private async Task<(AssistantMessage Result, List<AssistantMessageEvent> Events)> ExecuteAsync(string payload)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "text/event-stream")
        });

        var provider = CreateProvider(handler);
        var stream = provider.Stream(CreateModel(), CreateContext(), CreateOptions());
        var events = await ReadAllEventsAsync(stream);
        var result = await stream.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(10));

        handler.RequestCount.Should().Be(1);
        return (result, events);
    }

    private static async Task<List<AssistantMessageEvent>> ReadAllEventsAsync(LlmStream stream)
    {
        var events = new List<AssistantMessageEvent>();
        await foreach (var evt in stream)
            events.Add(evt);

        return events;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}
