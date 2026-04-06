using System.Text.Json.Serialization;

namespace BotNexus.Gateway.Api.WebSocket;

internal sealed record WsClientMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")] string? Content = null,
    [property: JsonPropertyName("sessionKey")] string? SessionKey = null,
    [property: JsonPropertyName("lastSeqId")] long? LastSeqId = null);
