using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using BotNexus.Domain.Serialization;

namespace BotNexus.Domain.Primitives;

[JsonConverter(typeof(SmartEnumJsonConverter<ExecutionStrategy>))]
/// <summary>
/// Represents execution strategy.
/// </summary>
public sealed class ExecutionStrategy : IEquatable<ExecutionStrategy>
{
    private static readonly ConcurrentDictionary<string, ExecutionStrategy> Registry = new(StringComparer.OrdinalIgnoreCase);

    public static readonly ExecutionStrategy InProcess = Register("in-process");
    public static readonly ExecutionStrategy Sandbox = Register("sandbox");
    public static readonly ExecutionStrategy Container = Register("container");
    public static readonly ExecutionStrategy Remote = Register("remote");

    /// <summary>
    /// Gets the value.
    /// </summary>
    public string Value { get; }

    private ExecutionStrategy(string value) => Value = value;

    /// <summary>
    /// Executes from string.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The from string result.</returns>
    public static ExecutionStrategy FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ExecutionStrategy cannot be empty", nameof(value));

        return Registry.GetOrAdd(value.Trim().ToLowerInvariant(), static v => new ExecutionStrategy(v));
    }

    /// <summary>
    /// Performs the declared conversion or operator operation.
    /// </summary>
    /// <param name="strategy">The strategy.</param>
    /// <returns>The operator string result.</returns>
    public static implicit operator string(ExecutionStrategy strategy) => strategy.Value;

    /// <summary>
    /// Executes equals.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>The equals result.</returns>
    public bool Equals(ExecutionStrategy? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Executes equals.
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <returns>The equals result.</returns>
    public override bool Equals(object? obj) => obj is ExecutionStrategy other && Equals(other);
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

    private static ExecutionStrategy Register(string value)
    {
        var strategy = new ExecutionStrategy(value);
        Registry.TryAdd(value, strategy);
        return strategy;
    }
}
