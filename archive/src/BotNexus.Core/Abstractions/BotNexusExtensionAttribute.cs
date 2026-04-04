namespace BotNexus.Core.Abstractions;

/// <summary>
/// Optional assembly-level attribute for extension metadata (display name, version, author).
/// Informational only — the loader does not require it.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class BotNexusExtensionAttribute : Attribute
{
    public string Name { get; }
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }

    public BotNexusExtensionAttribute(string name) => Name = name;
}
