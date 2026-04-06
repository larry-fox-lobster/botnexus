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

        return agentDirectory;
    }

    private static void ScaffoldAgentWorkspace(string agentDirectory)
    {
        foreach (var file in WorkspaceScaffoldFiles)
        {
            var path = Path.Combine(agentDirectory, file);
            if (!File.Exists(path))
                File.WriteAllText(path, string.Empty);
        }
    }
}
