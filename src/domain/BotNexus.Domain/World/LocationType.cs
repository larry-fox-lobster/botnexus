namespace BotNexus.Domain.World;

using BotNexus.Domain.Serialization;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

[JsonConverter(typeof(SmartEnumJsonConverter<LocationType>))]
/// <summary>
/// Represents location type.
/// </summary>
public sealed class LocationType : IEquatable<LocationType>
{
    private static readonly ConcurrentDictionary<string, LocationType> Registry = new(StringComparer.OrdinalIgnoreCase);

    public static readonly LocationType FileSystem = Register("filesystem");
    public static readonly LocationType Api = Register("api");
    public static readonly LocationType McpServer = Register("mcp-server");
    public static readonly LocationType RemoteNode = Register("remote-node");
    public static readonly LocationType Database = Register("database");

    /// <summary>
    /// Gets the value.
    /// </summary>
    public string Value { get; }

    private LocationType(string value) => Value = value;

    /// <summary>
    /// Executes from string.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The from string result.</returns>
    public static LocationType FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("LocationType cannot be empty", nameof(value));

        return Registry.GetOrAdd(value.Trim().ToLowerInvariant(), static v => new LocationType(v));
    }

    /// <summary>
    /// Performs the declared conversion or operator operation.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The operator string result.</returns>
    public static implicit operator string(LocationType type) => type.Value;

    /// <summary>
    /// Executes equals.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>The equals result.</returns>
    public bool Equals(LocationType? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Executes equals.
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <returns>The equals result.</returns>
    public override bool Equals(object? obj) => obj is LocationType other && Equals(other);
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

    private static LocationType Register(string value)
    {
        var type = new LocationType(value);
        Registry.TryAdd(value, type);
        return type;
    }
}
