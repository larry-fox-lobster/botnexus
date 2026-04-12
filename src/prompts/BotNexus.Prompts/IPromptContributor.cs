namespace BotNexus.Prompts;

public interface IPromptContributor
{
    PromptSection? Target { get; }

    int Priority { get; }

    bool ShouldInclude(PromptContext context);

    PromptContribution GetContribution(PromptContext context);
}
