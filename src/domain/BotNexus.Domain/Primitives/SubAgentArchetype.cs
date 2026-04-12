using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(SmartEnumJsonConverter<SubAgentArchetype>))]
/// <summary>
/// Represents sub agent archetype.
/// </summary>
public sealed class SubAgentArchetype : IEquatable<SubAgentArchetype>
{
    private static readonly ConcurrentDictionary<string, SubAgentArchetype> Registry = new(StringComparer.OrdinalIgnoreCase);

    public static readonly SubAgentArchetype Researcher = Register("researcher");
    public static readonly SubAgentArchetype Coder = Register("coder");
    public static readonly SubAgentArchetype Planner = Register("planner");
    public static readonly SubAgentArchetype Reviewer = Register("reviewer");
    public static readonly SubAgentArchetype Writer = Register("writer");
    public static readonly SubAgentArchetype General = Register("general");

    /// <summary>
    /// Gets the value.
    /// </summary>
    public string Value { get; }

    private SubAgentArchetype(string value) => Value = value;

    /// <summary>
    /// Executes from string.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The from string result.</returns>
    public static SubAgentArchetype FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SubAgentArchetype cannot be empty", nameof(value));

        return Registry.GetOrAdd(value.Trim().ToLowerInvariant(), static v => new SubAgentArchetype(v));
    }

    /// <summary>
    /// Performs the declared conversion or operator operation.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The operator sub agent archetype result.</returns>
    public static implicit operator SubAgentArchetype(string value) => FromString(value);
    /// <summary>
    /// Performs the declared conversion or operator operation.
    /// </summary>
    /// <param name="archetype">The archetype.</param>
    /// <returns>The operator string result.</returns>
    public static implicit operator string(SubAgentArchetype archetype) => archetype.Value;

    /// <summary>
    /// Executes equals.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>The equals result.</returns>
    public bool Equals(SubAgentArchetype? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Executes equals.
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <returns>The equals result.</returns>
    public override bool Equals(object? obj) => obj is SubAgentArchetype other && Equals(other);
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

    private static SubAgentArchetype Register(string value)
    {
        var archetype = new SubAgentArchetype(value);
        Registry.TryAdd(value, archetype);
        return archetype;
    }
}
