using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(ToolNameJsonConverter))]
public readonly record struct ToolName : IComparable<ToolName>, IEquatable<ToolName>
{
    public string Value { get; }

    public ToolName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ToolName cannot be empty", nameof(value));

        Value = value.Trim();
    }

    public static ToolName From(string value) => new(value);

    public static implicit operator string(ToolName toolName) => toolName.Value;
    public static explicit operator ToolName(string value) => From(value);

    public bool Equals(ToolName other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
    public override string ToString() => Value;
    public int CompareTo(ToolName other) => string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);
}
