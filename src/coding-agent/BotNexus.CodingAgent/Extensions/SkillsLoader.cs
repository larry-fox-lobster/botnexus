namespace BotNexus.CodingAgent.Extensions;

/// <summary>
/// Loads markdown skill instructions that are injected into system prompts.
/// </summary>
public sealed class SkillsLoader
{
    private const int MaxSkillNameLength = 64;

    public IReadOnlyList<string> LoadSkills(string workingDirectory, CodingAgentConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return [];
        }

        var root = Path.GetFullPath(workingDirectory);
        var skillDocuments = new List<string>();
        var knownSkillNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        TryAddFile(Path.Combine(root, "AGENTS.md"), skillDocuments);
        TryAddFile(Path.Combine(root, ".botnexus-agent", "AGENTS.md"), skillDocuments);

        var skillsDirectory = Path.Combine(root, ".botnexus-agent", "skills");
        if (Directory.Exists(skillsDirectory))
        {
            foreach (var skillPath in Directory.EnumerateFiles(skillsDirectory, "SKILL.md", SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                TryAddSkill(skillPath, skillDocuments, knownSkillNames);
            }
        }

        return skillDocuments;
    }

    private static void TryAddFile(string path, ICollection<string> target)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);
        if (!string.IsNullOrWhiteSpace(content))
        {
            target.Add(content);
        }
    }

    private static void TryAddSkill(string path, ICollection<string> target, ISet<string> knownSkillNames)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var parsed = ParseSkillDocument(path, content);
        if (parsed is null)
        {
            return;
        }

        if (!IsValidSkillName(parsed.Name))
        {
            Console.Error.WriteLine($"[warning] Ignoring skill '{parsed.Name}' from '{path}': invalid name format.");
            return;
        }

        if (!knownSkillNames.Add(parsed.Name))
        {
            Console.Error.WriteLine($"[warning] Duplicate skill name '{parsed.Name}' detected at '{path}'. Keeping first occurrence.");
            return;
        }

        if (parsed.DisableModelInvocation)
        {
            return;
        }

        var normalized = $"""
            ---
            name: {parsed.Name}
            description: {parsed.Description}
            disable-model-invocation: {parsed.DisableModelInvocation.ToString().ToLowerInvariant()}
            ---
            {parsed.Body}
            """;
        target.Add(normalized);
    }

    private static ParsedSkill? ParseSkillDocument(string path, string content)
    {
        var trimmed = content.Trim();
        var defaultName = Path.GetFileName(Path.GetDirectoryName(path) ?? path).ToLowerInvariant();

        if (!trimmed.StartsWith("---", StringComparison.Ordinal))
        {
            return new ParsedSkill(defaultName, $"Skill: {defaultName}", false, trimmed);
        }

        var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);
        var closingIndex = Array.FindIndex(lines, 1, line => line.Trim().Equals("---", StringComparison.Ordinal));
        if (closingIndex < 0)
        {
            return new ParsedSkill(defaultName, $"Skill: {defaultName}", false, trimmed);
        }

        var metadata = ParseFrontmatter(lines, closingIndex);
        var name = metadata.TryGetValue("name", out var rawName) && !string.IsNullOrWhiteSpace(rawName)
            ? rawName
            : defaultName;
        var description = metadata.TryGetValue("description", out var rawDescription) && !string.IsNullOrWhiteSpace(rawDescription)
            ? rawDescription
            : $"Skill: {name}";
        var disableModelInvocation = metadata.TryGetValue("disable-model-invocation", out var rawDisable)
            && bool.TryParse(rawDisable, out var parsedDisable)
            && parsedDisable;
        var body = string.Join(Environment.NewLine, lines.Skip(closingIndex + 1)).Trim();

        return new ParsedSkill(name, description, disableModelInvocation, body);
    }

    private static Dictionary<string, string> ParseFrontmatter(string[] lines, int closingIndex)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < closingIndex; index++)
        {
            var line = lines[index];
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"', '\'');
            if (!string.IsNullOrWhiteSpace(key))
            {
                metadata[key] = value;
            }
        }

        return metadata;
    }

    private static bool IsValidSkillName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxSkillNameLength)
        {
            return false;
        }

        return name.All(static character => char.IsLower(character) || char.IsDigit(character) || character == '-');
    }

    private sealed record ParsedSkill(string Name, string Description, bool DisableModelInvocation, string Body);
}
