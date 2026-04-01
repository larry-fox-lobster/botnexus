namespace BotNexus.Core.Configuration;

public static class BotNexusHome
{
    private const string HomeDirectoryName = ".botnexus";
    private const string HomeOverrideEnvVar = "BOTNEXUS_HOME";

    private const string DefaultConfigJson = """
{
  "BotNexus": {
    "ExtensionsPath": "~/.botnexus/extensions",
    "Providers": {
      "copilot": {
        "Auth": "oauth",
        "ApiBase": "https://api.githubcopilot.com"
      }
    }
  }
}
""";

    public static string ResolveHomePath()
    {
        var homeOverride = Environment.GetEnvironmentVariable(HomeOverrideEnvVar);
        if (!string.IsNullOrWhiteSpace(homeOverride))
            return ResolveAbsolutePath(homeOverride);

        return ResolveAbsolutePath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            HomeDirectoryName));
    }

    public static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var normalized = path.Trim();
        if (normalized.StartsWith("~/.botnexus", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("~\\.botnexus", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = normalized[11..].TrimStart('\\', '/');
            var home = ResolveHomePath();
            return string.IsNullOrEmpty(suffix)
                ? home
                : ResolveAbsolutePath(Path.Combine(home, suffix));
        }

        return ResolveAbsolutePath(normalized);
    }

    public static string Initialize()
    {
        var homePath = ResolveHomePath();
        Directory.CreateDirectory(homePath);
        Directory.CreateDirectory(Path.Combine(homePath, "extensions"));
        Directory.CreateDirectory(Path.Combine(homePath, "extensions", "providers"));
        Directory.CreateDirectory(Path.Combine(homePath, "extensions", "channels"));
        Directory.CreateDirectory(Path.Combine(homePath, "extensions", "tools"));
        Directory.CreateDirectory(Path.Combine(homePath, "tokens"));
        Directory.CreateDirectory(Path.Combine(homePath, "sessions"));
        Directory.CreateDirectory(Path.Combine(homePath, "logs"));

        var configPath = Path.Combine(homePath, "config.json");
        if (!File.Exists(configPath))
            File.WriteAllText(configPath, DefaultConfigJson);

        return homePath;
    }

    private static string ResolveAbsolutePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (expanded.StartsWith("~", StringComparison.Ordinal))
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expanded = string.Concat(userHome, expanded[1..]);
        }

        return Path.IsPathRooted(expanded)
            ? Path.GetFullPath(expanded)
            : Path.GetFullPath(expanded, Directory.GetCurrentDirectory());
    }
}
