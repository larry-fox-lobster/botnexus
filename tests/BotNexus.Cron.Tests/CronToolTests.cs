using System.Text.Json;
using BotNexus.AgentCore.Types;
using BotNexus.Cron.Tools;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BotNexus.Cron.Tests;

public sealed class CronToolTests
{
    [Fact]
    public async Task ExecuteAsync_List_ReturnsJobs()
    {
        var store = new Mock<ICronStore>();
        var scheduler = CreateScheduler();
        store.Setup(value => value.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateJob("job-1", createdBy: "agent-a"),
                CreateJob("job-2", createdBy: "agent-b")
            ]);
        var tool = new CronTool(store.Object, scheduler, "agent-a");

        var result = await tool.ExecuteAsync("call-1", new Dictionary<string, object?> { ["action"] = "list" });
        var jobs = JsonSerializer.Deserialize<List<CronJobDto>>(ReadText(result), JsonOptions);

        jobs.Should().NotBeNull();
        jobs!.Should().ContainSingle(job => job.Id == "job-1");
    }

    [Fact]
    public async Task ExecuteAsync_Create_CreatesJob()
    {
        var store = new Mock<ICronStore>();
        var scheduler = CreateScheduler();
        CronJob? created = null;
        store.Setup(value => value.CreateAsync(It.IsAny<CronJob>(), It.IsAny<CancellationToken>()))
            .Callback<CronJob, CancellationToken>((job, _) => created = job)
            .ReturnsAsync((CronJob job, CancellationToken _) => job);
        var tool = new CronTool(store.Object, scheduler, "agent-a");

        var result = await tool.ExecuteAsync("call-1", new Dictionary<string, object?>
        {
            ["action"] = "create",
            ["name"] = "Daily summary",
            ["schedule"] = "*/5 * * * *",
            ["message"] = "Summarize status"
        });

        created.Should().NotBeNull();
        created!.ActionType.Should().Be("agent-prompt");
        created.CreatedBy.Should().Be("agent-a");
        created.AgentId.Should().Be("agent-a");
        ReadText(result).Should().Contain("Daily summary");
    }

    [Fact]
    public async Task ExecuteAsync_Delete_OwnedJob_Succeeds()
    {
        var store = new Mock<ICronStore>();
        var scheduler = CreateScheduler();
        store.Setup(value => value.GetAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateJob("job-1", createdBy: "agent-a"));
        store.Setup(value => value.DeleteAsync("job-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var tool = new CronTool(store.Object, scheduler, "agent-a");

        var result = await tool.ExecuteAsync("call-1", new Dictionary<string, object?>
        {
            ["action"] = "delete",
            ["jobId"] = "job-1"
        });

        ReadText(result).Should().Contain("Deleted cron job 'job-1'");
        store.Verify(value => value.DeleteAsync("job-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Delete_OtherAgentJob_Denied()
    {
        var store = new Mock<ICronStore>();
        var scheduler = CreateScheduler();
        store.Setup(value => value.GetAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateJob("job-1", createdBy: "other-agent"));
        var tool = new CronTool(store.Object, scheduler, "agent-a");

        var act = () => tool.ExecuteAsync("call-1", new Dictionary<string, object?>
        {
            ["action"] = "delete",
            ["jobId"] = "job-1"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static CronScheduler CreateScheduler()
    {
        var store = new Mock<ICronStore>().Object;
        var scopeFactory = new ServiceCollection()
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var options = new StaticOptionsMonitor<CronOptions>(new CronOptions());
        return new CronScheduler(
            store,
            Array.Empty<ICronAction>(),
            scopeFactory,
            options,
            NullLogger<CronScheduler>.Instance);
    }

    private static CronJob CreateJob(string id, string createdBy)
        => new()
        {
            Id = id,
            Name = $"Job {id}",
            Schedule = "*/1 * * * *",
            ActionType = "agent-prompt",
            AgentId = "agent-a",
            Message = "Hello",
            Enabled = true,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static string ReadText(AgentToolResult result)
        => result.Content.Single(content => content.Type == AgentToolContentType.Text).Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class CronJobDto
    {
        public string Id { get; set; } = string.Empty;
    }

    private sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = currentValue;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
