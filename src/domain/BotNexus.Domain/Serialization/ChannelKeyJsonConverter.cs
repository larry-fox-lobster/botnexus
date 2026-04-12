using System.Text.Json;
using System.Text.Json.Serialization;
using BotNexus.Domain.Primitives;

namespace BotNexus.Domain.Serialization;

public sealed class ChannelKeyJsonConverter : JsonConverter<ChannelKey>
{
    public override ChannelKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("ChannelKey must be a string.");

        return ChannelKey.From(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, ChannelKey value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
