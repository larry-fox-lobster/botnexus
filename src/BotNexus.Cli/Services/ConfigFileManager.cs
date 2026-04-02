using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotNexus.Core.Configuration;

namespace BotNexus.Cli.Services;

public sealed class ConfigFileManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BotNexusConfig LoadConfig(string homePath)
    {
        var defaults = new BotNexusConfig();
        var configPath = Path.Combine(homePath, "config.json");
        if (!File.Exists(configPath))
            return defaults;

        try
        {
            var json = File.ReadAllText(configPath);
            if (string.IsNullOrWhiteSpace(json))
                return defaults;

            var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            BotNexusConfig? parsed = null;

            if (root.TryGetProperty(BotNexusConfig.SectionName, out var section))
                parsed = section.Deserialize<BotNexusConfig>(JsonOptions);
            else if (root.ValueKind == JsonValueKind.Object)
                parsed = root.Deserialize<BotNexusConfig>(JsonOptions);

            return MergeConfig(defaults, parsed ?? new BotNexusConfig());
        }
        catch (JsonException)
        {
            return defaults;
        }
    }

    public void SaveConfig(string homePath, BotNexusConfig config)
    {
        Directory.CreateDirectory(homePath);
        var payload = new Dictionary<string, BotNexusConfig>
        {
            [BotNexusConfig.SectionName] = config
        };

        var configPath = Path.Combine(homePath, "config.json");
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        File.WriteAllText(configPath, json);
    }

    public BotNexusConfig MergeConfig(BotNexusConfig existing, BotNexusConfig overlay)
    {
        var result = JsonSerializer.Deserialize<BotNexusConfig>(
            JsonSerializer.Serialize(existing, JsonOptions),
            JsonOptions) ?? new BotNexusConfig();

        MergeObject(result, overlay);
        return result;
    }

    private static void MergeObject(object target, object overlay)
    {
        var targetType = target.GetType();
        foreach (var property in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite)
                continue;

            var overlayValue = property.GetValue(overlay);
            if (overlayValue is null)
                continue;

            var targetValue = property.GetValue(target);
            if (targetValue is not null && IsComplexType(property.PropertyType))
            {
                MergeObject(targetValue, overlayValue);
                continue;
            }

            property.SetValue(target, overlayValue);
        }
    }

    private static bool IsComplexType(Type type)
        => type.IsClass && type != typeof(string) && !typeof(JsonNode).IsAssignableFrom(type);
}
