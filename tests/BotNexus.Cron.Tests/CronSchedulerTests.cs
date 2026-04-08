using System.Reflection;
using BotNexus.Cron.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BotNexus.Cron.Tests;

public sealed class CronSchedulerTests
{
    [Fact]
    public async Task Scheduler_ExecutesDueJobs()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        var action = new RecordingAction("test-action");
        var job = CronStoreTestContext.CreateJob("job-1", actionType: "test-action") with
        {
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        await context.Store.CreateAsync(job);
        var scheduler = CreateScheduler(context.Store, [action]);

        await InvokeProcessTickAsync(scheduler);

        action.ExecutionCount.Should().Be(1);
        var history = await context.Store.GetRunHistoryAsync("job-1");
        history.Should().ContainSingle(run => run.Status == "ok");
    }

    [Fact]
    public async Task Scheduler_SkipsDisabledJobs()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        var action = new RecordingAction("test-action");
        var job = CronStoreTestContext.CreateJob("job-1", actionType: "test-action", enabled: false) with
        {
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        await context.Store.CreateAsync(job);
        var scheduler = CreateScheduler(context.Store, [action]);

        await InvokeProcessTickAsync(scheduler);

        action.ExecutionCount.Should().Be(0);
        (await context.Store.GetRunHistoryAsync("job-1")).Should().BeEmpty();
    }

    [Fact]
    public async Task Scheduler_RecordsRunOnSuccess()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        var action = new RecordingAction("test-action");
        var job = CronStoreTestContext.CreateJob("job-1", actionType: "test-action");
        await context.Store.CreateAsync(job);
        var scheduler = CreateScheduler(context.Store, [action]);

        var run = await scheduler.RunNowAsync("job-1");

        run.Status.Should().Be("ok");
        var updated = await context.Store.GetAsync("job-1");
        updated!.LastRunStatus.Should().Be("ok");
        updated.LastRunError.Should().BeNull();
        var history = await context.Store.GetRunHistoryAsync("job-1");
        history.Should().ContainSingle(entry => entry.Status == "ok");
    }

    [Fact]
    public async Task Scheduler_RecordsErrorOnFailure()
    {
        await using var context = await CronStoreTestContext.CreateAsync();
        var action = new ThrowingAction("test-action", "boom");
        var job = CronStoreTestContext.CreateJob("job-1", actionType: "test-action");
        await context.Store.CreateAsync(job);
        var scheduler = CreateScheduler(context.Store, [action]);

        var run = await scheduler.RunNowAsync("job-1");

        run.Status.Should().Be("error");
        run.Error.Should().Be("boom");
        var updated = await context.Store.GetAsync("job-1");
        updated!.LastRunStatus.Should().Be("error");
        updated.LastRunError.Should().Contain("boom");
        var history = await context.Store.GetRunHistoryAsync("job-1");
        history.Should().ContainSingle(entry => entry.Status == "error" && entry.Error == "boom");
    }

    private static CronScheduler CreateScheduler(ICronStore store, IEnumerable<ICronAction> actions)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        return new CronScheduler(
            store,
            actions,
            scopeFactory,
            new StaticOptionsMonitor<CronOptions>(new CronOptions { Enabled = true, TickIntervalSeconds = 1 }),
            NullLogger<CronScheduler>.Instance);
    }

    private static async Task InvokeProcessTickAsync(CronScheduler scheduler)
    {
        var method = typeof(CronScheduler).GetMethod("ProcessTickAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();
        var task = method!.Invoke(scheduler, [CancellationToken.None]) as Task;
        task.Should().NotBeNull();
        await task!;
    }

    private sealed class RecordingAction(string actionType) : ICronAction
    {
        public int ExecutionCount { get; private set; }
        public string ActionType => actionType;

        public Task ExecuteAsync(CronExecutionContext context, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAction(string actionType, string message) : ICronAction
    {
        public string ActionType => actionType;

        public Task ExecuteAsync(CronExecutionContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(message);
    }

    private sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = currentValue;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
