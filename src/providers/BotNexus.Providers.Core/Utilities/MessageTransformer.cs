using BotNexus.Providers.Core.Models;

namespace BotNexus.Providers.Core.Utilities;

/// <summary>
/// Cross-provider message transformation.
/// Port of pi-mono's providers/transform-messages.ts.
/// </summary>
public static class MessageTransformer
{
    /// <summary>
    /// Transform messages for cross-provider compatibility.
    /// - Converts thinking blocks to text when switching providers
    /// - Normalizes tool call IDs
    /// - Inserts synthetic tool results for orphaned tool calls
    /// - Skips errored/aborted assistant messages
    /// </summary>
    public static List<Message> TransformMessages(
        IReadOnlyList<Message> messages,
        LlmModel targetModel,
        Func<string, string>? normalizeToolCallId = null)
    {
        var result = new List<Message>();
        var pendingToolCallIds = new HashSet<string>();
        var isSameModel = IsSameModel(messages, targetModel);

        foreach (var message in messages)
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    // Skip errored/aborted messages
                    if (assistant.StopReason is StopReason.Error or StopReason.Aborted)
                        continue;

                    var transformedContent = TransformAssistantContent(
                        assistant.Content, isSameModel, normalizeToolCallId);

                    // Track tool calls that need results
                    foreach (var block in transformedContent)
                    {
                        if (block is ToolCallContent tc)
                            pendingToolCallIds.Add(tc.Id);
                    }

                    result.Add(assistant with { Content = transformedContent });
                    break;

                case ToolResultMessage toolResult:
                    var toolCallId = normalizeToolCallId is not null
                        ? normalizeToolCallId(toolResult.ToolCallId)
                        : toolResult.ToolCallId;

                    pendingToolCallIds.Remove(toolCallId);
                    result.Add(toolResult with { ToolCallId = toolCallId });
                    break;

                default:
                    // Flush any orphaned tool calls before adding next message
                    FlushOrphanedToolCalls(result, pendingToolCallIds);
                    result.Add(message);
                    break;
            }
        }

        // Flush any remaining orphaned tool calls
        FlushOrphanedToolCalls(result, pendingToolCallIds);

        return result;
    }

    private static bool IsSameModel(IReadOnlyList<Message> messages, LlmModel targetModel)
    {
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i] is AssistantMessage assistant)
                return assistant.Provider == targetModel.Provider && assistant.Api == targetModel.Api;
        }
        return true;
    }

    private static List<ContentBlock> TransformAssistantContent(
        IReadOnlyList<ContentBlock> content,
        bool isSameModel,
        Func<string, string>? normalizeToolCallId)
    {
        var transformed = new List<ContentBlock>(content.Count);

        foreach (var block in content)
        {
            switch (block)
            {
                case ThinkingContent thinking when !isSameModel:
                    // Convert thinking to text with delimiters when crossing providers
                    transformed.Add(new TextContent($"<thinking>\n{thinking.Thinking}\n</thinking>"));
                    break;

                case ToolCallContent tc when normalizeToolCallId is not null:
                    transformed.Add(tc with { Id = normalizeToolCallId(tc.Id) });
                    break;

                default:
                    transformed.Add(block);
                    break;
            }
        }

        return transformed;
    }

    private static void FlushOrphanedToolCalls(List<Message> result, HashSet<string> pendingToolCallIds)
    {
        if (pendingToolCallIds.Count == 0)
            return;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var orphanId in pendingToolCallIds)
        {
            result.Add(new ToolResultMessage(
                ToolCallId: orphanId,
                ToolName: "unknown",
                Content: [new TextContent("[Tool call result not available]")],
                IsError: true,
                Timestamp: timestamp
            ));
        }

        pendingToolCallIds.Clear();
    }
}
