using System.Text.Json;
using System.Text.Json.Serialization;
using BotNexus.Domain.Primitives;

namespace BotNexus.Domain.Serialization;

public sealed class AgentSessionKeyJsonConverter : JsonConverter<AgentSessionKey>
{
    public override AgentSessionKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("AgentSessionKey must be a string.");

        return AgentSessionKey.Parse(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, AgentSessionKey value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
