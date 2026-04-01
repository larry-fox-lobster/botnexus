namespace BotNexus.Core.Abstractions;

/// <summary>
/// Registry for pluggable system actions.
/// </summary>
public interface ISystemActionRegistry
{
    /// <summary>
    /// Registers a system action.
    /// </summary>
    void Register(ISystemAction action);

    /// <summary>
    /// Gets a registered action by name.
    /// </summary>
    ISystemAction? Get(string name);

    /// <summary>
    /// Gets all registered actions.
    /// </summary>
    IReadOnlyList<ISystemAction> GetAll();
}
