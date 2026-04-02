using System.Reflection;
using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using Microsoft.Extensions.Options;

namespace BotNexus.Diagnostics.Checkups.Extensions;

public sealed class ExtensionAssembliesValidCheckup(IOptions<BotNexusConfig> options) : IHealthCheckup
{
    private readonly BotNexusConfig _config = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public string Name => "ExtensionAssembliesValid";
    public string Category => "Extensions";
    public string Description => "Checks extension DLL files are valid .NET assemblies.";

    public Task<CheckupResult> RunAsync(CancellationToken ct = default)
    {
        try
        {
            var extensionRoot = BotNexusHome.ResolvePath(_config.ExtensionsPath);
            var extensionFolders = GetConfiguredExtensionFolders(extensionRoot).Where(Directory.Exists).ToList();
            if (extensionFolders.Count == 0)
            {
                return Task.FromResult(new CheckupResult(
                    CheckupStatus.Warn,
                    "No configured extension folders exist to validate assemblies.",
                    "Create extension folders and place extension assemblies before running this check."));
            }

            var invalidAssemblies = new List<string>();
            var checkedAssemblies = 0;

            foreach (var folder in extensionFolders)
            {
                foreach (var dll in Directory.EnumerateFiles(folder, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    checkedAssemblies++;
                    try
                    {
                        _ = AssemblyName.GetAssemblyName(dll);
                    }
                    catch (Exception ex) when (ex is FileLoadException or FileNotFoundException or BadImageFormatException)
                    {
                        invalidAssemblies.Add($"{dll} ({ex.Message})");
                    }
                }
            }

            if (invalidAssemblies.Count > 0)
            {
                return Task.FromResult(new CheckupResult(
                    CheckupStatus.Fail,
                    $"Invalid extension assemblies: {string.Join("; ", invalidAssemblies)}",
                    "Rebuild or replace invalid DLLs with valid .NET assemblies."));
            }

            if (checkedAssemblies == 0)
            {
                return Task.FromResult(new CheckupResult(
                    CheckupStatus.Warn,
                    "No extension DLL files were found in configured extension folders.",
                    "Add extension DLLs under each configured extension folder."));
            }

            return Task.FromResult(new CheckupResult(
                CheckupStatus.Pass,
                $"Validated {checkedAssemblies} extension assembly file(s)."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CheckupResult(
                CheckupStatus.Fail,
                $"Failed to validate extension assemblies: {ex.Message}",
                "Inspect extension folders and DLL files under BotNexus:ExtensionsPath."));
        }
    }

    private IEnumerable<string> GetConfiguredExtensionFolders(string extensionRoot)
    {
        foreach (var provider in _config.Providers.Keys)
            yield return Path.Combine(extensionRoot, "providers", provider);

        foreach (var (channelName, channelConfig) in _config.Channels.Instances)
        {
            if (channelConfig.Enabled)
                yield return Path.Combine(extensionRoot, "channels", channelName);
        }

        foreach (var tool in _config.Tools.Extensions.Keys)
            yield return Path.Combine(extensionRoot, "tools", tool);
    }
}
