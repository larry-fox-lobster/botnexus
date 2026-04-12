using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(SmartEnumJsonConverter<SessionStatus>))]
public sealed class SessionStatus : IEquatable<SessionStatus>
{
    private static readonly ConcurrentDictionary<string, SessionStatus> Registry = new(StringComparer.OrdinalIgnoreCase);

    public static readonly SessionStatus Active = Register("active");
    public static readonly SessionStatus Suspended = Register("suspended");
    public static readonly SessionStatus Sealed = Register("sealed");

    public string Value { get; }

    private SessionStatus(string value) => Value = value;

    public static SessionStatus FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SessionStatus cannot be empty", nameof(value));

        return Registry.GetOrAdd(value.Trim().ToLowerInvariant(), static v => new SessionStatus(v));
    }

    public static implicit operator string(SessionStatus status) => status.Value;

    public bool Equals(SessionStatus? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is SessionStatus other && Equals(other);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
    public override string ToString() => Value;

    private static SessionStatus Register(string value)
    {
        var status = new SessionStatus(value);
        Registry.TryAdd(value, status);
        return status;
    }
}
