namespace BotNexus.Cli;

/// <summary>
/// Canonical path resolution for CLI source and target directories.
/// </summary>
internal static class CliPaths
{
    /// <summary>
    /// Default source (repo) location: ~/botnexus
    /// </summary>
    public static string DefaultSource => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "botnexus");

    /// <summary>
    /// Default target (runtime home) location: ~/.botnexus
    /// </summary>
    public static string DefaultTarget => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".botnexus");

    /// <summary>
    /// Resolve the source directory. Explicit path wins; otherwise DefaultSource.
    /// </summary>
    public static string ResolveSource(string? explicitSource) =>
        string.IsNullOrWhiteSpace(explicitSource) ? DefaultSource : explicitSource;

    /// <summary>
    /// Resolve the target (BotNexus home) directory. Explicit path wins; otherwise DefaultTarget.
    /// </summary>
    public static string ResolveTarget(string? explicitTarget) =>
        string.IsNullOrWhiteSpace(explicitTarget) ? DefaultTarget : explicitTarget;
}
