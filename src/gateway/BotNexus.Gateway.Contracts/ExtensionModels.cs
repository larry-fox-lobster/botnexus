namespace BotNexus.Gateway.Abstractions.Extensions;

/// <summary>
/// Manifest format stored in botnexus-extension.json.
/// </summary>
public sealed record ExtensionManifest
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public string Version { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the entry assembly.
    /// </summary>
    public string EntryAssembly { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the extension types.
    /// </summary>
    public IReadOnlyList<string> ExtensionTypes { get; init; } = [];
    /// <summary>
    /// Gets or sets the dependencies.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];
}

/// <summary>
/// Metadata for a discovered extension on disk.
/// </summary>
public sealed record ExtensionInfo
{
    /// <summary>
    /// Gets or sets the directory path.
    /// </summary>
    public required string DirectoryPath { get; init; }
    /// <summary>
    /// Gets or sets the manifest path.
    /// </summary>
    public required string ManifestPath { get; init; }
    /// <summary>
    /// Gets or sets the entry assembly path.
    /// </summary>
    public required string EntryAssemblyPath { get; init; }
    /// <summary>
    /// Gets or sets the manifest.
    /// </summary>
    public required ExtensionManifest Manifest { get; init; }
}

/// <summary>
/// Result of attempting to load an extension.
/// </summary>
public sealed record ExtensionLoadResult
{
    /// <summary>
    /// Gets or sets the extension id.
    /// </summary>
    public required string ExtensionId { get; init; }
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    public required bool Success { get; init; }
    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    public string? Error { get; init; }
    /// <summary>
    /// Gets or sets the registered services.
    /// </summary>
    public IReadOnlyList<string> RegisteredServices { get; init; } = [];
}

/// <summary>
/// Runtime information about an extension that is currently loaded.
/// </summary>
public sealed record LoadedExtension
{
    /// <summary>
    /// Gets or sets the extension id.
    /// </summary>
    public required string ExtensionId { get; init; }
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public required string Version { get; init; }
    /// <summary>
    /// Gets or sets the directory path.
    /// </summary>
    public required string DirectoryPath { get; init; }
    /// <summary>
    /// Gets or sets the entry assembly path.
    /// </summary>
    public required string EntryAssemblyPath { get; init; }
    /// <summary>
    /// Gets or sets the extension types.
    /// </summary>
    public IReadOnlyList<string> ExtensionTypes { get; init; } = [];
    /// <summary>
    /// Gets or sets the loaded at utc.
    /// </summary>
    public required DateTimeOffset LoadedAtUtc { get; init; }
    /// <summary>
    /// Gets or sets the registered services.
    /// </summary>
    public IReadOnlyList<string> RegisteredServices { get; init; } = [];
}
