using System.Text.Json;
using System.Text.Json.Serialization;
using BotNexus.Domain.Primitives;

namespace BotNexus.Domain.Serialization;

public sealed class AgentIdJsonConverter : JsonConverter<AgentId>
{
    public override AgentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("AgentId must be a string.");

        return AgentId.From(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, AgentId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
