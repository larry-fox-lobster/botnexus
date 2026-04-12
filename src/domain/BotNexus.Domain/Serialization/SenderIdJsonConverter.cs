using System.Text.Json;
using System.Text.Json.Serialization;
using BotNexus.Domain.Primitives;

namespace BotNexus.Domain.Serialization;

public sealed class SenderIdJsonConverter : JsonConverter<SenderId>
{
    public override SenderId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("SenderId must be a string.");

        return SenderId.From(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, SenderId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
