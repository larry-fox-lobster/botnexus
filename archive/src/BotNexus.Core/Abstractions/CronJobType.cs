namespace BotNexus.Core.Abstractions;

/// <summary>Discriminator for cron job categories.</summary>
public enum CronJobType
{
    /// <summary>Runs a prompt through an agent via AgentRunner.</summary>
    Agent,

    /// <summary>Runs a system action directly (no agent, no LLM).</summary>
    System,

    /// <summary>Runs internal maintenance (memory consolidation, log rotation, session cleanup).</summary>
    Maintenance
}
