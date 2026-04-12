using System.Text.Json;
using System.Text.Json.Serialization;
using BotNexus.Domain.Primitives;

namespace BotNexus.Domain.Serialization;

public sealed class ConversationIdJsonConverter : JsonConverter<ConversationId>
{
    public override ConversationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("ConversationId must be a string.");

        return ConversationId.From(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, ConversationId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
