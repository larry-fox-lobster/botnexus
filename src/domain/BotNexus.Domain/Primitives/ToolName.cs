using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(ToolNameJsonConverter))]
/// <summary>
/// Represents struct.
/// </summary>
public readonly record struct ToolName : IComparable<ToolName>, IEquatable<ToolName>
{
    /// <summary>
    /// Gets the value.
    /// </summary>
    public string Value { get; }

    public ToolName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ToolName cannot be empty", nameof(value));

        Value = value.Trim();
    }

    /// <summary>
    /// Executes from.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The from result.</returns>
    public static ToolName From(string value) => new(value);

    /// <summary>
    /// Performs the declared conversion or operator operation.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <returns>The operator string result.</returns>
    public static implicit operator string(ToolName toolName) => toolName.Value;
    /// <summary>
    /// Performs the declared conversion or operator operation.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The operator tool name result.</returns>
    public static explicit operator ToolName(string value) => From(value);

    /// <summary>
    /// Executes equals.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>The equals result.</returns>
    public bool Equals(ToolName other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Executes get hash code.
    /// </summary>
    /// <returns>The get hash code result.</returns>
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
    /// <summary>
    /// Executes to string.
    /// </summary>
    /// <returns>The to string result.</returns>
    public override string ToString() => Value;
    /// <summary>
    /// Executes compare to.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>The compare to result.</returns>
    public int CompareTo(ToolName other) => string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);
}
