using System.Text.Json;
using System.Text.Json.Serialization;
using BotNexus.Domain.Primitives;

namespace BotNexus.Domain.Serialization;

public sealed class ToolNameJsonConverter : JsonConverter<ToolName>
{
    public override ToolName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("ToolName must be a string.");

        return ToolName.From(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, ToolName value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
