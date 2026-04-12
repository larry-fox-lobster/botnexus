using System.Text.Json;
using System.Text.Json.Serialization;
using BotNexus.Domain.Primitives;

namespace BotNexus.Domain.Serialization;

public sealed class SessionIdJsonConverter : JsonConverter<SessionId>
{
    public override SessionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("SessionId must be a string.");

        return SessionId.From(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, SessionId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
