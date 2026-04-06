namespace BotNexus.Gateway.Configuration;

public sealed class BotNexusHome(string? homePath = null)
{
    public const string HomeOverrideEnvVar = "BOTNEXUS_HOME";
    private const string HomeDirectoryName = ".botnexus";

    private static readonly string[] RequiredDirectories =
    [
        "extensions",
        "tokens",
        "sessions",
        "logs",
        "agents"
    ];

    private static readonly string[] WorkspaceScaffoldFiles =
    [
        "SOUL.md",
        "IDENTITY.md",
        "USER.md",
        "MEMORY.md"
    ];

    public string RootPath { get; } = ResolveHomePath(homePath);

    public string AgentsPath => Path.Combine(RootPath, "agents");

    public static string ResolveHomePath(string? homePath = null)
    {
        if (!string.IsNullOrWhiteSpace(homePath))
            return Path.GetFullPath(homePath);

        var homeOverride = Environment.GetEnvironmentVariable(HomeOverrideEnvVar);
        if (!string.IsNullOrWhiteSpace(homeOverride))
            return Path.GetFullPath(homeOverride);

        return Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            HomeDirectoryName));
    }

    public void Initialize()
    {
        Directory.CreateDirectory(RootPath);
        foreach (var directory in RequiredDirectories)
            Directory.CreateDirectory(Path.Combine(RootPath, directory));
    }

    public string GetAgentDirectory(string agentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        Initialize();
        var agentDirectory = Path.Combine(AgentsPath, agentName.Trim());
        var isFirstCreation = !Directory.Exists(agentDirectory);
        Directory.CreateDirectory(agentDirectory);

        if (isFirstCreation)
            ScaffoldAgentWorkspace(agentDirectory);
        else
            MigrateLegacyWorkspace(agentDirectory);

        return agentDirectory;
    }

    private static void ScaffoldAgentWorkspace(string agentDirectory)
    {
        var workspacePath = Path.Combine(agentDirectory, "workspace");
        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(Path.Combine(agentDirectory, "data", "sessions"));
        foreach (var file in WorkspaceScaffoldFiles)
        {
            var path = Path.Combine(workspacePath, file);
            if (!File.Exists(path))
                File.WriteAllText(path, string.Empty);
        }
    }

    private static void MigrateLegacyWorkspace(string agentDirectory)
    {
        var workspacePath = Path.Combine(agentDirectory, "workspace");
        if (Directory.Exists(workspacePath))
            return;

        var hasLegacyFiles = WorkspaceScaffoldFiles
            .Any(f => File.Exists(Path.Combine(agentDirectory, f)));
        if (!hasLegacyFiles)
        {
            ScaffoldAgentWorkspace(agentDirectory);
            return;
        }

        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(Path.Combine(agentDirectory, "data", "sessions"));
        foreach (var file in WorkspaceScaffoldFiles)
        {
            var src = Path.Combine(agentDirectory, file);
            var dst = Path.Combine(workspacePath, file);
            if (File.Exists(src))
                File.Move(src, dst);
        }
    }
}
