using System.Runtime.InteropServices;

namespace BotNexus.Gateway.Prompts;

/// <summary>
/// Represents environment info.
/// </summary>
public static class EnvironmentInfo
{
    /// <summary>
    /// Executes build section.
    /// </summary>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="gitBranch">The git branch.</param>
    /// <param name="gitStatus">The git status.</param>
    /// <param name="packageManager">The package manager.</param>
    /// <returns>The build section result.</returns>
    public static IReadOnlyList<string> BuildSection(string workingDirectory, string? gitBranch, string? gitStatus, string packageManager)
    {
        return
        [
            $"- OS: {RuntimeInformation.OSDescription}",
            $"- Working directory: {workingDirectory.Replace('\\', '/')}",
            $"- Git branch: {gitBranch ?? "N/A"}",
            $"- Git status: {gitStatus ?? "N/A"}",
            $"- Package manager: {packageManager}"
        ];
    }
}
