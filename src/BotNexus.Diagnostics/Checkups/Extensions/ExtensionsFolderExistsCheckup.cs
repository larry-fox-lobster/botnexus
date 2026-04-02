using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using Microsoft.Extensions.Options;

namespace BotNexus.Diagnostics.Checkups.Extensions;

public sealed class ExtensionsFolderExistsCheckup(IOptions<BotNexusConfig> options) : IHealthCheckup
{
    private readonly BotNexusConfig _config = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public string Name => "ExtensionsFolderExists";
    public string Category => "Extensions";
    public string Description => "Checks configured extension folders exist under the extensions root.";
    public bool CanAutoFix => true;

    public Task<CheckupResult> RunAsync(CancellationToken ct = default)
    {
        try
        {
            var extensionRoot = BotNexusHome.ResolvePath(_config.ExtensionsPath);
            var configuredFolders = GetConfiguredExtensionFolders(extensionRoot).ToList();
            if (configuredFolders.Count == 0)
            {
                return Task.FromResult(new CheckupResult(
                    CheckupStatus.Warn,
                    "No extensions are configured.",
                    "Configure providers, enabled channels, or tool extensions to run this check."));
            }

            var missing = configuredFolders.Where(path => !Directory.Exists(path)).ToList();
            if (missing.Count > 0)
            {
                return Task.FromResult(new CheckupResult(
                    CheckupStatus.Fail,
                    $"Missing extension folders: {string.Join(", ", missing)}",
                    "Create missing extension folders under extensions/providers, extensions/channels, or extensions/tools."));
            }

            return Task.FromResult(new CheckupResult(
                CheckupStatus.Pass,
                $"All {configuredFolders.Count} configured extension folder(s) exist."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CheckupResult(
                CheckupStatus.Fail,
                $"Failed to validate extension folders: {ex.Message}",
                "Verify BotNexus:ExtensionsPath and configured extension keys."));
        }
    }

    public Task<CheckupResult> FixAsync(CancellationToken ct = default)
    {
        var extensionRoot = BotNexusHome.ResolvePath(_config.ExtensionsPath);
        foreach (var folder in GetConfiguredExtensionFolders(extensionRoot).Distinct(StringComparer.OrdinalIgnoreCase))
            Directory.CreateDirectory(folder);

        return RunAsync(ct);
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
