namespace BotNexus.Core.Abstractions;

/// <summary>
/// Represents a pluggable system action invoked by scheduled jobs.
/// </summary>
public interface ISystemAction
{
    /// <summary>
    /// Unique action name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what the action does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the system action.
    /// </summary>
    Task<string> ExecuteAsync(CancellationToken cancellationToken = default);
}
