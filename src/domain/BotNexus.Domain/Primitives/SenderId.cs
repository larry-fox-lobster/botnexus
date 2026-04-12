using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(SenderIdJsonConverter))]
/// <summary>
/// Represents struct.
/// </summary>
public readonly record struct SenderId(string Value) : IComparable<SenderId>
{
    /// <summary>
    /// Executes from.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The from result.</returns>
    public static SenderId From(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("SenderId cannot be empty", nameof(value))
            : new(value.Trim());

    /// <summary>
    /// Performs the declared conversion or operator operation.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The operator string result.</returns>
    public static implicit operator string(SenderId id) => id.Value;
    /// <summary>
    /// Performs the declared conversion or operator operation.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The operator sender id result.</returns>
    public static explicit operator SenderId(string value) => From(value);

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
    public int CompareTo(SenderId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
}
