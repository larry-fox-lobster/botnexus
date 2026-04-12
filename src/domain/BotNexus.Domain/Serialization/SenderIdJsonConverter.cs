using System.Text.Json;
using System.Text.Json.Serialization;
using BotNexus.Domain.Primitives;

namespace BotNexus.Domain.Serialization;

/// <summary>
/// Represents sender id json converter.
/// </summary>
public sealed class SenderIdJsonConverter : JsonConverter<SenderId>
{
    /// <summary>
    /// Executes read.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">The options.</param>
    /// <returns>The read result.</returns>
    public override SenderId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("SenderId must be a string.");

        return SenderId.From(reader.GetString() ?? string.Empty);
    }

    /// <summary>
    /// Executes write.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    /// <param name="options">The options.</param>
    public override void Write(Utf8JsonWriter writer, SenderId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
