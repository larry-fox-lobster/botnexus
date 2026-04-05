using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BotNexus.Gateway.Configuration;

/// <summary>
/// Loads and validates platform configuration from ~/.botnexus/config.json.
/// </summary>
public static class PlatformConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>The default platform configuration directory.</summary>
    public static string DefaultConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".botnexus");

    /// <summary>The default configuration file path.</summary>
    public static string DefaultConfigPath =>
        Path.Combine(DefaultConfigDirectory, "config.json");

    /// <summary>Loads config from the default path, returning defaults if file doesn't exist.</summary>
    public static async Task<PlatformConfig> LoadAsync(string? configPath = null, CancellationToken cancellationToken = default)
    {
        var path = configPath ?? DefaultConfigPath;
        if (!File.Exists(path))
            return new PlatformConfig();

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<PlatformConfig>(stream, JsonOptions, cancellationToken)
            ?? new PlatformConfig();
    }

    /// <summary>Validates the configuration and returns any errors.</summary>
    public static IReadOnlyList<string> Validate(PlatformConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        List<string> errors = [];
        Uri? listenUri = null;

        if (!string.IsNullOrWhiteSpace(config.ListenUrl) &&
            !Uri.TryCreate(config.ListenUrl, UriKind.Absolute, out listenUri))
        {
            errors.Add("ListenUrl must be a valid absolute URL.");
        }
        else if (listenUri is not null && !(listenUri.Scheme == Uri.UriSchemeHttp || listenUri.Scheme == Uri.UriSchemeHttps))
        {
            errors.Add("ListenUrl must use http or https.");
        }

        ValidatePath(config.AgentsDirectory, nameof(config.AgentsDirectory), errors);
        ValidatePath(config.SessionsDirectory, nameof(config.SessionsDirectory), errors);

        if (!string.IsNullOrWhiteSpace(config.LogLevel) &&
            !Enum.TryParse<LogLevel>(config.LogLevel, ignoreCase: true, out _))
        {
            errors.Add("LogLevel must be one of: Trace, Debug, Information, Warning, Error, Critical.");
        }

        return errors;
    }

    /// <summary>Ensures the .botnexus directory exists.</summary>
    public static void EnsureConfigDirectory(string? configDir = null)
    {
        var directory = string.IsNullOrWhiteSpace(configDir) ? DefaultConfigDirectory : configDir;
        Directory.CreateDirectory(directory);
    }

    private static void ValidatePath(string? path, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            errors.Add($"{fieldName} contains invalid path characters.");
            return;
        }

        try
        {
            _ = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            errors.Add($"{fieldName} must be a valid path.");
        }
    }
}
