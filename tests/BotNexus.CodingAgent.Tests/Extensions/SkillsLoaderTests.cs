using BotNexus.CodingAgent.Extensions;
using FluentAssertions;

namespace BotNexus.CodingAgent.Tests.Extensions;

public sealed class SkillsLoaderTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"botnexus-skillsloader-{Guid.NewGuid():N}");

    public SkillsLoaderTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void LoadSkills_WhenMarkdownIsNotSkillFile_IgnoresIt()
    {
        var skillsRoot = Path.Combine(_tempDirectory, ".botnexus-agent", "skills");
        Directory.CreateDirectory(skillsRoot);
        File.WriteAllText(Path.Combine(skillsRoot, "note.md"), "not a skill");

        var skills = new SkillsLoader().LoadSkills(_tempDirectory, new CodingAgentConfig());

        skills.Should().BeEmpty();
    }

    [Fact]
    public void LoadSkills_WhenSkillUsesFrontmatter_UsesDeclaredNameAndDescription()
    {
        var skillDirectory = Path.Combine(_tempDirectory, ".botnexus-agent", "skills", "example");
        Directory.CreateDirectory(skillDirectory);
        File.WriteAllText(Path.Combine(skillDirectory, "SKILL.md"), """
            ---
            name: my-skill
            description: Sample skill
            disable-model-invocation: false
            ---
            Skill body
            """);

        var skills = new SkillsLoader().LoadSkills(_tempDirectory, new CodingAgentConfig());

        skills.Should().ContainSingle();
        skills[0].Should().Contain("name: my-skill");
        skills[0].Should().Contain("description: Sample skill");
        skills[0].Should().Contain("Skill body");
    }

    [Fact]
    public void LoadSkills_WhenSkillNameInvalid_SkipsSkill()
    {
        var skillDirectory = Path.Combine(_tempDirectory, ".botnexus-agent", "skills", "BadSkill");
        Directory.CreateDirectory(skillDirectory);
        File.WriteAllText(Path.Combine(skillDirectory, "SKILL.md"), """
            ---
            name: BadSkill
            description: Invalid
            ---
            Body
            """);

        var skills = new SkillsLoader().LoadSkills(_tempDirectory, new CodingAgentConfig());

        skills.Should().BeEmpty();
    }

    [Fact]
    public void LoadSkills_WhenDuplicateNamesExist_KeepsFirstOccurrence()
    {
        var firstDirectory = Path.Combine(_tempDirectory, ".botnexus-agent", "skills", "first");
        var secondDirectory = Path.Combine(_tempDirectory, ".botnexus-agent", "skills", "second");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        File.WriteAllText(Path.Combine(firstDirectory, "SKILL.md"), """
            ---
            name: duplicate-skill
            description: First
            ---
            First body
            """);
        File.WriteAllText(Path.Combine(secondDirectory, "SKILL.md"), """
            ---
            name: duplicate-skill
            description: Second
            ---
            Second body
            """);

        var skills = new SkillsLoader().LoadSkills(_tempDirectory, new CodingAgentConfig());

        skills.Should().ContainSingle();
        skills[0].Should().Contain("description: First");
        skills[0].Should().Contain("First body");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
