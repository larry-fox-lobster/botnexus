using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(SmartEnumJsonConverter<ExecutionStrategy>))]
public sealed class ExecutionStrategy : IEquatable<ExecutionStrategy>
{
    private static readonly ConcurrentDictionary<string, ExecutionStrategy> Registry = new(StringComparer.OrdinalIgnoreCase);

    public static readonly ExecutionStrategy InProcess = Register("in-process");
    public static readonly ExecutionStrategy Sandbox = Register("sandbox");
    public static readonly ExecutionStrategy Container = Register("container");
    public static readonly ExecutionStrategy Remote = Register("remote");

    public string Value { get; }

    private ExecutionStrategy(string value) => Value = value;

    public static ExecutionStrategy FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ExecutionStrategy cannot be empty", nameof(value));

        return Registry.GetOrAdd(value.Trim().ToLowerInvariant(), static v => new ExecutionStrategy(v));
    }

    public static implicit operator string(ExecutionStrategy strategy) => strategy.Value;

    public bool Equals(ExecutionStrategy? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is ExecutionStrategy other && Equals(other);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
    public override string ToString() => Value;

    private static ExecutionStrategy Register(string value)
    {
        var strategy = new ExecutionStrategy(value);
        Registry.TryAdd(value, strategy);
        return strategy;
    }
}
