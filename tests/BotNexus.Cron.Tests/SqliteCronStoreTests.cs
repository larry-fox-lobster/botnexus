using BotNexus.Cron.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace BotNexus.Cron.Tests;

public sealed class SqliteCronStoreTests
{
    [Fact]
    public async Task InitializeAsync_CreatesSchema()
    {
        await using var context = await CronStoreTestContext.CreateAsync();

        await context.Store.InitializeAsync();

        await using var connection = new SqliteConnection($"Data Source={context.DbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
            """;

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        tables.Should().Contain(["cron_jobs", "cron_runs"]);
    }

    [Fact]
    public async Task CreateAsync_StoresAndRetrievesByid()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        var job = CronStoreTestContext.CreateJob("job-1");

        await context.Store.CreateAsync(job);
        var loaded = await context.Store.GetAsync("job-1");

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be("job-1");
        loaded.Name.Should().Be(job.Name);
        loaded.AgentId.Should().Be("agent-a");
    }

    [Fact]
    public async Task ListAsync_ReturnsAllJobs()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        await context.Store.CreateAsync(CronStoreTestContext.CreateJob("job-1", "agent-a"));
        await context.Store.CreateAsync(CronStoreTestContext.CreateJob("job-2", "agent-b"));

        var jobs = await context.Store.ListAsync();

        jobs.Should().HaveCount(2);
        jobs.Select(job => job.Id).Should().BeEquivalentTo("job-1", "job-2");
    }

    [Fact]
    public async Task ListAsync_FiltersByAgentId()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        await context.Store.CreateAsync(CronStoreTestContext.CreateJob("job-1", "agent-a"));
        await context.Store.CreateAsync(CronStoreTestContext.CreateJob("job-2", "agent-b"));

        var filtered = await context.Store.ListAsync("agent-a");

        filtered.Should().ContainSingle();
        filtered[0].Id.Should().Be("job-1");
    }

    [Fact]
    public async Task UpdateAsync_ModifiesJob()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        await context.Store.CreateAsync(CronStoreTestContext.CreateJob("job-1"));

        var updated = CronStoreTestContext.CreateJob("job-1") with
        {
            Name = "Updated Name",
            Enabled = false,
            LastRunStatus = "ok"
        };
        await context.Store.UpdateAsync(updated);

        var loaded = await context.Store.GetAsync("job-1");
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Updated Name");
        loaded.Enabled.Should().BeFalse();
        loaded.LastRunStatus.Should().Be("ok");
    }

    [Fact]
    public async Task DeleteAsync_RemovesJob()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        await context.Store.CreateAsync(CronStoreTestContext.CreateJob("job-1"));

        await context.Store.DeleteAsync("job-1");

        (await context.Store.GetAsync("job-1")).Should().BeNull();
    }

    [Fact]
    public async Task RecordRunStartAsync_CreatesRunEntry()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        await context.Store.CreateAsync(CronStoreTestContext.CreateJob("job-1"));

        var run = await context.Store.RecordRunStartAsync("job-1");

        run.JobId.Should().Be("job-1");
        run.Status.Should().Be("running");
        var history = await context.Store.GetRunHistoryAsync("job-1");
        history.Should().ContainSingle(entry => entry.Id == run.Id && entry.Status == "running");
    }

    [Fact]
    public async Task RecordRunCompleteAsync_UpdatesRunStatus()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        await context.Store.CreateAsync(CronStoreTestContext.CreateJob("job-1"));
        var run = await context.Store.RecordRunStartAsync("job-1");

        await context.Store.RecordRunCompleteAsync(run.Id, "ok", sessionId: "session-1");
        var history = await context.Store.GetRunHistoryAsync("job-1");

        history.Should().ContainSingle();
        history[0].Status.Should().Be("ok");
        history[0].SessionId.Should().Be("session-1");
        history[0].CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRunHistoryAsync_ReturnsRunsForJob()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        await context.Store.CreateAsync(CronStoreTestContext.CreateJob("job-1"));
        await context.Store.CreateAsync(CronStoreTestContext.CreateJob("job-2"));

        var run1 = await context.Store.RecordRunStartAsync("job-1");
        await context.Store.RecordRunCompleteAsync(run1.Id, "ok");
        var run2 = await context.Store.RecordRunStartAsync("job-2");
        await context.Store.RecordRunCompleteAsync(run2.Id, "error", "boom");

        var history = await context.Store.GetRunHistoryAsync("job-1");

        history.Should().ContainSingle();
        history[0].JobId.Should().Be("job-1");
        history[0].Status.Should().Be("ok");
    }
}
