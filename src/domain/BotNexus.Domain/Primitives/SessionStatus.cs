using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(SmartEnumJsonConverter<SessionStatus>))]
/// <summary>
/// Represents session status.
/// </summary>
public sealed class SessionStatus : IEquatable<SessionStatus>
{
    private static readonly ConcurrentDictionary<string, SessionStatus> Registry = new(StringComparer.OrdinalIgnoreCase);

    public static readonly SessionStatus Active = Register("active");
    public static readonly SessionStatus Suspended = Register("suspended");
    public static readonly SessionStatus Sealed = Register("sealed");

    /// <summary>
    /// Gets the value.
    /// </summary>
    public string Value { get; }

    private SessionStatus(string value) => Value = value;

    /// <summary>
    /// Executes from string.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The from string result.</returns>
    public static SessionStatus FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SessionStatus cannot be empty", nameof(value));

        return Registry.GetOrAdd(value.Trim().ToLowerInvariant(), static v => new SessionStatus(v));
    }

    /// <summary>
    /// Performs the declared conversion or operator operation.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The operator string result.</returns>
    public static implicit operator string(SessionStatus status) => status.Value;

    /// <summary>
    /// Executes equals.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>The equals result.</returns>
    public bool Equals(SessionStatus? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Executes equals.
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <returns>The equals result.</returns>
    public override bool Equals(object? obj) => obj is SessionStatus other && Equals(other);
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

    private static SessionStatus Register(string value)
    {
        var status = new SessionStatus(value);
        Registry.TryAdd(value, status);
        return status;
    }
}
