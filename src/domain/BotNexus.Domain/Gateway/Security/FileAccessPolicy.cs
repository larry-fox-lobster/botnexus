namespace BotNexus.Gateway.Abstractions.Security;

/// <summary>
/// Represents file access policy.
/// </summary>
public sealed record FileAccessPolicy
{
    /// <summary>
    /// Gets or sets the allowed read paths.
    /// </summary>
    public IReadOnlyList<string> AllowedReadPaths { get; init; } = [];
    /// <summary>
    /// Gets or sets the allowed write paths.
    /// </summary>
    public IReadOnlyList<string> AllowedWritePaths { get; init; } = [];
    /// <summary>
    /// Gets or sets the denied paths.
    /// </summary>
    public IReadOnlyList<string> DeniedPaths { get; init; } = [];
}
