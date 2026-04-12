using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotNexus.Domain.Serialization;

/// <summary>
/// Represents smart enum json converter.
/// </summary>
public sealed class SmartEnumJsonConverter<TSmartEnum> : JsonConverter<TSmartEnum>
    where TSmartEnum : class
{
    private static readonly MethodInfo? FromStringMethod =
        typeof(TSmartEnum).GetMethod("FromString", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);

    /// <summary>
    /// Executes read.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">The options.</param>
    /// <returns>The read result.</returns>
    public override TSmartEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected JSON string for {typeof(TSmartEnum).Name}.");

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException($"{typeof(TSmartEnum).Name} cannot be empty.");

        if (FromStringMethod is null)
            throw new JsonException($"{typeof(TSmartEnum).Name} must expose a public static FromString(string) method.");

        var result = FromStringMethod.Invoke(null, [value]);
        return result as TSmartEnum
            ?? throw new JsonException($"Unable to convert '{value}' to {typeof(TSmartEnum).Name}.");
    }

    /// <summary>
    /// Executes write.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    /// <param name="options">The options.</param>
    public override void Write(Utf8JsonWriter writer, TSmartEnum value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
