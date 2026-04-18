namespace BotNexus.Gateway.Prompts;

/// <summary>
/// Defines the contract for iprompt contributor.
/// </summary>
public interface IPromptContributor
{
    PromptSection? Target { get; }

    int Priority { get; }

    bool ShouldInclude(PromptContext context);

    PromptContribution GetContribution(PromptContext context);
}
