using BotNexus.Core.Abstractions;
using BotNexus.Core.Models;

namespace BotNexus.Agent.Tools;

/// <summary>Tool for sending messages back to a channel from within an agent.</summary>
public sealed class MessageTool : ITool
{
    private readonly IChannel? _channel;

    public MessageTool(IChannel? channel = null)
    {
        _channel = channel;
    }

    /// <inheritdoc/>
    public ToolDefinition Definition => new(
        "send_message",
        "Send a message to a specific chat or channel.",
        new Dictionary<string, ToolParameterSchema>
        {
            ["chat_id"] = new("string", "The chat ID to send the message to", Required: true),
            ["content"] = new("string", "The message content to send", Required: true),
            ["channel"] = new("string", "Optional channel name override", Required: false)
        });

    /// <inheritdoc/>
    public async Task<string> ExecuteAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var chatId = arguments.GetValueOrDefault("chat_id")?.ToString() ?? string.Empty;
        var content = arguments.GetValueOrDefault("content")?.ToString() ?? string.Empty;
        var channelName = arguments.GetValueOrDefault("channel")?.ToString();

        if (_channel is null)
            return "Error: No channel available";

        var message = new OutboundMessage(channelName ?? _channel.Name, chatId, content);
        await _channel.SendAsync(message, cancellationToken).ConfigureAwait(false);
        return "Message sent successfully";
    }
}
