namespace BotNexus.CodingAgent.Utils;

public static class PackageManagerDetector
{
    public static string Detect(string workingDir)
    {
        var root = Path.GetFullPath(workingDir);

        if (File.Exists(Path.Combine(root, "pnpm-lock.yaml")))
        {
            return "pnpm";
        }

        if (File.Exists(Path.Combine(root, "yarn.lock")))
        {
            return "yarn";
        }

        if (File.Exists(Path.Combine(root, "package-lock.json")))
        {
            return "npm";
        }

        if (File.Exists(Path.Combine(root, "go.mod")))
        {
            return "go";
        }

        if (File.Exists(Path.Combine(root, "Cargo.toml")))
        {
            return "cargo";
        }

        if (File.Exists(Path.Combine(root, "pom.xml")))
        {
            return "maven";
        }

        if (File.Exists(Path.Combine(root, "build.gradle")) || File.Exists(Path.Combine(root, "build.gradle.kts")))
        {
            return "gradle";
        }

        if (Directory.EnumerateFiles(root, "*.sln", SearchOption.TopDirectoryOnly).Any()
            || Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).Any())
        {
            return "dotnet";
        }

        if (File.Exists(Path.Combine(root, "Gemfile.lock")))
        {
            return "bundler";
        }

        if (File.Exists(Path.Combine(root, "requirements.txt")) || File.Exists(Path.Combine(root, "pyproject.toml")))
        {
            return "python";
        }

        return "unknown";
    }
}
