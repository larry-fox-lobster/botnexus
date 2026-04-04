namespace BotNexus.Core.Configuration;

public class ExtensionLoadingConfig
{
    public bool RequireSignedAssemblies { get; set; }
    public int MaxAssembliesPerExtension { get; set; } = 50;
    public bool DryRun { get; set; }
}
