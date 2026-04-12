using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(ConversationIdJsonConverter))]
public readonly record struct ConversationId(string Value) : IComparable<ConversationId>
{
    public static ConversationId From(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("ConversationId cannot be empty", nameof(value))
            : new(value.Trim());

    public static implicit operator string(ConversationId id) => id.Value;
    public static explicit operator ConversationId(string value) => From(value);

    public override string ToString() => Value;
    public int CompareTo(ConversationId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
}
