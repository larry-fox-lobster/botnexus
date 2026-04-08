namespace BotNexus.Cron.Actions;

public sealed class AgentPromptAction : ICronAction
{
    public string ActionType => "agent-prompt";

    public Task ExecuteAsync(CronExecutionContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
