using System.Collections.Concurrent;
using BotNexus.Gateway.Abstractions.Models;
using FluentAssertions;

namespace BotNexus.Gateway.Tests;

public sealed class GatewaySessionThreadSafetyTests
{
    [Fact]
    public async Task AddEntry_WithConcurrentWriters_DoesNotCorruptHistory()
    {
        var session = CreateSession();
        const int totalEntries = 500;

        var writers = Enumerable.Range(0, totalEntries)
            .Select(i => Task.Run(() => session.AddEntry(new SessionEntry { Role = "user", Content = $"entry-{i}" })));
        await Task.WhenAll(writers);

        var snapshot = session.GetHistorySnapshot();
        snapshot.Should().HaveCount(totalEntries);
        snapshot.Select(e => e.Content).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetHistorySnapshot_DuringConcurrentMutation_ReturnsConsistentSnapshots()
    {
        var session = CreateSession();
        var cts = new CancellationTokenSource();
        var errors = new ConcurrentQueue<Exception>();

        var writer = Task.Run(async () =>
        {
            for (var i = 0; i < 250; i++)
            {
                session.AddEntry(new SessionEntry { Role = "assistant", Content = $"m-{i}" });
                await Task.Yield();
            }

            cts.Cancel();
        });

        var reader = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var snapshot = session.GetHistorySnapshot();
                    snapshot.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Role));
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }
        });

        await Task.WhenAll(writer, reader);
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task AddEntries_WhenObservedConcurrently_IsAtomicPerBatch()
    {
        var session = CreateSession();
        const int batchSize = 4;
        const int batchCount = 100;
        var cts = new CancellationTokenSource();
        var errors = new ConcurrentQueue<string>();

        var writer = Task.Run(async () =>
        {
            for (var batch = 0; batch < batchCount; batch++)
            {
                var batchEntries = Enumerable.Range(0, batchSize)
                    .Select(i => new SessionEntry { Role = "user", Content = $"batch-{batch}-entry-{i}" });
                session.AddEntries(batchEntries);
                await Task.Yield();
            }

            cts.Cancel();
        });

        var reader = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                var snapshot = session.GetHistorySnapshot();
                var grouped = snapshot
                    .GroupBy(e =>
                    {
                        var markerIndex = e.Content.LastIndexOf("-entry-", StringComparison.Ordinal);
                        return markerIndex >= 0 ? e.Content[..markerIndex] : e.Content;
                    });

                foreach (var group in grouped)
                {
                    if (group.Count() != batchSize)
                    {
                        errors.Enqueue($"Batch '{group.Key}' had non-atomic visibility with {group.Count()} entries.");
                    }
                }
            }
        });

        await Task.WhenAll(writer, reader);
        errors.Should().BeEmpty();
    }

    private static GatewaySession CreateSession()
        => new() { SessionId = $"session-{Guid.NewGuid():N}", AgentId = "agent-a" };
}
