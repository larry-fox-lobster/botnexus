namespace BotNexus.Core.Abstractions;

/// <summary>
/// Consolidates daily memory files into long-term MEMORY.md using LLM summarization.
/// Triggered by heartbeat service on a configurable interval.
/// </summary>
public interface IMemoryConsolidator
{
    /// <summary>
    /// Consolidate daily memory files for an agent into long-term memory.
    /// Reads old daily files, summarizes via LLM, updates MEMORY.md, archives processed dailies.
    /// </summary>
    Task<MemoryConsolidationResult> ConsolidateAsync(string agentName, CancellationToken cancellationToken = default);
}

/// <summary>Result of a memory consolidation operation.</summary>
public sealed record MemoryConsolidationResult(
    bool Success,
    int DailyFilesProcessed,
    int EntriesConsolidated,
    string? Error = null);
