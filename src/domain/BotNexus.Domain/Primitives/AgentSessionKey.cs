using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(AgentSessionKeyJsonConverter))]
public readonly record struct AgentSessionKey(AgentId AgentId, SessionId SessionId)
{
    public static AgentSessionKey From(AgentId agentId, SessionId sessionId) => new(agentId, sessionId);

    public static AgentSessionKey Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("AgentSessionKey cannot be empty", nameof(value));

        var parts = value.Split("::", StringSplitOptions.None);
        if (parts.Length < 2)
            throw new ArgumentException("AgentSessionKey must be in '{agentId}::{sessionId}' format.", nameof(value));

        return new AgentSessionKey(
            AgentId.From(parts[0]),
            SessionId.From(string.Join("::", parts.Skip(1)).Trim()));
    }

    public override string ToString() => $"{AgentId}::{SessionId}";
}
