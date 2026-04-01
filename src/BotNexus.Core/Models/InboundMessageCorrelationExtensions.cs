namespace BotNexus.Core.Models;

/// <summary>Helpers for correlation IDs on inbound messages.</summary>
public static class InboundMessageCorrelationExtensions
{
    public const string CorrelationIdMetadataKey = "correlation_id";

    public static InboundMessage EnsureCorrelationId(this InboundMessage message, out string correlationId)
    {
        correlationId = message.GetCorrelationId() ?? Guid.NewGuid().ToString("N");
        if (message.Metadata.TryGetValue(CorrelationIdMetadataKey, out _))
            return message;

        var metadata = new Dictionary<string, object>(message.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            [CorrelationIdMetadataKey] = correlationId
        };

        return message with { Metadata = metadata };
    }

    public static string? GetCorrelationId(this InboundMessage message)
    {
        if (!message.Metadata.TryGetValue(CorrelationIdMetadataKey, out var value) || value is null)
            return null;

        if (value is string text && !string.IsNullOrWhiteSpace(text))
            return text;

        var converted = Convert.ToString(value);
        return string.IsNullOrWhiteSpace(converted) ? null : converted;
    }
}
