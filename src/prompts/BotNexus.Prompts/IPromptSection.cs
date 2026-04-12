namespace BotNexus.Prompts;

/// <summary>
/// Defines the contract for iprompt section.
/// </summary>
public interface IPromptSection
{
    int Order { get; }

    bool ShouldInclude(PromptContext context);

    IReadOnlyList<string> Build(PromptContext context);
}
