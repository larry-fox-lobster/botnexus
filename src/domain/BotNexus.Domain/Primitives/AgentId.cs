using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(AgentIdJsonConverter))]
/// <summary>
/// Represents struct.
/// </summary>
public readonly record struct AgentId(string Value) : IComparable<AgentId>
{
    /// <summary>
    /// Executes from.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The from result.</returns>
    public static AgentId From(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("AgentId cannot be empty", nameof(value))
            : new(value.Trim());

    /// <summary>
    /// Performs the declared conversion or operator operation.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The operator string result.</returns>
    public static implicit operator string(AgentId id) => id.Value;
    /// <summary>
    /// Performs the declared conversion or operator operation.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The operator agent id result.</returns>
    public static implicit operator AgentId(string value) => From(value);

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
    public int CompareTo(AgentId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
}
