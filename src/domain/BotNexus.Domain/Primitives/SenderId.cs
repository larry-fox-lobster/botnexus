using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(SenderIdJsonConverter))]
public readonly record struct SenderId(string Value) : IComparable<SenderId>
{
    public static SenderId From(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("SenderId cannot be empty", nameof(value))
            : new(value.Trim());

    public static implicit operator string(SenderId id) => id.Value;
    public static explicit operator SenderId(string value) => From(value);

    public override string ToString() => Value;
    public int CompareTo(SenderId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
}
