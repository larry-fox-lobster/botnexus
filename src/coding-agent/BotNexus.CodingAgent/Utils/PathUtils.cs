using System.Diagnostics;

namespace BotNexus.CodingAgent.Utils;

/// <summary>
/// Provides path normalization, containment validation, and repository ignore checks for coding-agent tools.
/// </summary>
/// <remarks>
/// <para>
/// All public helpers enforce a single invariant: file system operations must stay inside the configured
/// working directory root. This prevents accidental or malicious path traversal beyond the repository boundary.
/// </para>
/// <para>
/// These methods throw <see cref="InvalidOperationException"/> when a caller provides unsafe input.
/// Tool implementations intentionally surface those exceptions to the agent loop as structured tool errors.
/// </para>
/// </remarks>
public static class PathUtils
{
    /// <summary>
    /// Resolves a user-provided path against a working directory while enforcing root containment.
    /// </summary>
    /// <param name="relative">The user path, absolute or relative.</param>
    /// <param name="workingDirectory">The repository root used as the containment boundary.</param>
    /// <returns>A normalized absolute path guaranteed to remain under <paramref name="workingDirectory"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when inputs are empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when path traversal escapes the root boundary.</exception>
    public static string ResolvePath(string relative, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            throw new ArgumentException("Path cannot be empty.", nameof(relative));
        }

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("Working directory cannot be empty.", nameof(workingDirectory));
        }

        var root = Path.GetFullPath(workingDirectory);
        var sanitizedInput = SanitizePath(relative);

        var resolved = Path.IsPathRooted(sanitizedInput)
            ? Path.GetFullPath(sanitizedInput)
            : Path.GetFullPath(Path.Combine(root, sanitizedInput));

        if (!IsUnderRoot(resolved, root))
        {
            throw new InvalidOperationException(
                $"Path '{relative}' resolves outside working directory '{root}'.");
        }

        return resolved;
    }

    /// <summary>
    /// Normalizes separators and validates that parent-directory traversal does not escape the current root.
    /// </summary>
    /// <param name="path">The raw path value from tool input.</param>
    /// <returns>A sanitized path with canonical separators and collapsed <c>.</c>/<c>..</c> segments.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when traversal attempts to escape root scope.</exception>
    public static string SanitizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty.", nameof(path));
        }

        var normalizedSeparators = path.Trim()
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalizedSeparators))
        {
            var root = Path.GetPathRoot(normalizedSeparators)
                       ?? throw new InvalidOperationException("Unable to resolve path root.");

            var suffix = normalizedSeparators[root.Length..];
            var normalizedSuffix = NormalizeSegments(suffix);
            var rooted = Path.Combine(root, normalizedSuffix);
            return Path.GetFullPath(rooted);
        }

        return NormalizeSegments(normalizedSeparators);
    }

    /// <summary>
    /// Returns a display-friendly relative path from <paramref name="basePath"/> to <paramref name="fullPath"/>.
    /// </summary>
    /// <param name="fullPath">The full path to convert.</param>
    /// <param name="basePath">The base path to compute relativity from.</param>
    /// <returns>A relative path suitable for user-facing output.</returns>
    public static string GetRelativePath(string fullPath, string basePath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            throw new ArgumentException("Full path cannot be empty.", nameof(fullPath));
        }

        if (string.IsNullOrWhiteSpace(basePath))
        {
            throw new ArgumentException("Base path cannot be empty.", nameof(basePath));
        }

        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedBasePath = Path.GetFullPath(basePath);
        return Path.GetRelativePath(normalizedBasePath, normalizedFullPath);
    }

    /// <summary>
    /// Checks whether Git excludes the supplied path according to repository ignore rules.
    /// </summary>
    /// <param name="path">Path to evaluate. Can be absolute or relative.</param>
    /// <param name="workingDirectory">Repository working directory.</param>
    /// <returns>
    /// <see langword="true"/> when Git reports the path as ignored; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Exit-code semantics from <c>git check-ignore -q</c>:
    /// 0 = ignored, 1 = not ignored, anything else = git/runtime error.
    /// Errors are treated as non-ignored to keep tool behavior deterministic.
    /// </remarks>
    public static bool IsGitIgnored(string path, string workingDirectory)
    {
        var resolvedPath = ResolvePath(path, workingDirectory);
        var relativePath = GetRelativePath(resolvedPath, workingDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"-C \"{workingDirectory}\" check-ignore -q -- \"{relativePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        if (!process.WaitForExit(5000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort timeout cleanup; ignore secondary failures.
            }

            return false;
        }

        return process.ExitCode == 0;
    }

    private static string NormalizeSegments(string path)
    {
        var stack = new Stack<string>();
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (stack.Count == 0)
                {
                    throw new InvalidOperationException($"Path traversal is not allowed: '{path}'.");
                }

                stack.Pop();
                continue;
            }

            stack.Push(segment);
        }

        return string.Join(Path.DirectorySeparatorChar, stack.Reverse());
    }

    private static bool IsUnderRoot(string path, string root)
    {
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        var normalizedPath = Path.GetFullPath(path);

        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalizedPath.TrimEnd(Path.DirectorySeparatorChar), normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
