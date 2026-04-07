using System.Text.Json.Serialization;

namespace BotNexus.Gateway.Api.WebSocket;

internal sealed record WsClientMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")] string? Content = null,
    [property: JsonPropertyName("agentId")] string? AgentId = null,
    [property: JsonPropertyName("sessionId")] string? SessionId = null,
    [property: JsonPropertyName("includeHistory")] bool? IncludeHistory = null,
    [property: JsonPropertyName("historyLimit")] int? HistoryLimit = null,
    [property: JsonPropertyName("sessionKey")] string? SessionKey = null,
    [property: JsonPropertyName("lastSeqId")] long? LastSeqId = null);
