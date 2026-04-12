namespace BotNexus.Prompts;

public interface IPromptSection
{
    int Order { get; }

    bool ShouldInclude(PromptContext context);

    IReadOnlyList<string> Build(PromptContext context);
}
