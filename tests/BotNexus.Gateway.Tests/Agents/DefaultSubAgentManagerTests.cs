using System.Collections.Concurrent;
using BotNexus.Gateway.Abstractions.Agents;
using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Configuration;
using FluentAssertions;
using Moq;

namespace BotNexus.Gateway.Tests.Agents;

public sealed class DefaultSubAgentManagerTests
{
    [Fact]
    public async Task SpawnAsync_CreatesSubAgentSession()
    {
        var childHandle = CreateHandle();
        string? capturedSessionId = null;
        var supervisor = new Mock<IAgentSupervisor>();
        supervisor
            .Setup(s => s.GetOrCreateAsync("parent-agent", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, sessionId, _) =>
            {
                if (sessionId.Contains("::sub::", StringComparison.Ordinal))
                {
                    capturedSessionId = sessionId;
                }
            })
            .ReturnsAsync(childHandle.Object);

        var manager = CreateScaffoldManager(supervisor.Object);

        await manager.SpawnAsync(CreateSpawnRequest());

        capturedSessionId.Should().NotBeNull();
        capturedSessionId.Should().StartWith("parent-session::sub::");
    }

    [Fact]
    public async Task SpawnAsync_ReturnsSubAgentInfo_WithRunningStatus()
    {
        var childHandle = CreateHandle();
        var supervisor = new Mock<IAgentSupervisor>();
        supervisor
            .Setup(s => s.GetOrCreateAsync("parent-agent", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(childHandle.Object);

        var manager = CreateScaffoldManager(supervisor.Object);

        var result = await manager.SpawnAsync(CreateSpawnRequest());

        result.SubAgentId.Should().NotBeNullOrWhiteSpace();
        result.ParentSessionId.Should().Be("parent-session");
        result.ChildSessionId.Should().StartWith("parent-session::sub::");
        result.Task.Should().Be("Investigate timeout");
        result.Status.Should().Be(SubAgentStatus.Running);
    }

    [Fact]
    public async Task SpawnAsync_EnforcesConcurrentLimit()
    {
        var hangingHandle = CreateHangingHandle();
        var supervisor = new Mock<IAgentSupervisor>();
        supervisor
            .Setup(s => s.GetOrCreateAsync("parent-agent", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hangingHandle.Object);

        var manager = CreateScaffoldManager(
            supervisor.Object,
            new SubAgentOptions { MaxConcurrentPerSession = 1 });

        _ = await manager.SpawnAsync(CreateSpawnRequest());
        var act = () => manager.SpawnAsync(CreateSpawnRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*concurrent*");
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyChildrenOfParent()
    {
        var childHandle = CreateHandle();
        var supervisor = new Mock<IAgentSupervisor>();
        supervisor
            .Setup(s => s.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(childHandle.Object);

        var manager = CreateScaffoldManager(supervisor.Object);

        _ = await manager.SpawnAsync(CreateSpawnRequest(parentSessionId: "parent-a"));
        _ = await manager.SpawnAsync(CreateSpawnRequest(parentSessionId: "parent-a"));
        _ = await manager.SpawnAsync(CreateSpawnRequest(parentSessionId: "parent-b"));

        var result = await manager.ListAsync("parent-a");

        result.Should().HaveCount(2);
        result.Should().OnlyContain(info => info.ParentSessionId == "parent-a");
    }

    [Fact]
    public async Task ListAsync_ReturnsEmpty_WhenNoSubAgents()
    {
        var manager = CreateScaffoldManager(new Mock<IAgentSupervisor>().Object);

        var result = await manager.ListAsync("missing-parent");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task KillAsync_StopsSubAgent_AndUpdatesStatus()
    {
        var childHandle = CreateHangingHandle();
        var supervisor = new Mock<IAgentSupervisor>();
        supervisor
            .Setup(s => s.GetOrCreateAsync("parent-agent", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(childHandle.Object);
        supervisor
            .Setup(s => s.StopAsync("parent-agent", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var manager = CreateScaffoldManager(supervisor.Object);
        var spawned = await manager.SpawnAsync(CreateSpawnRequest());

        var killed = await manager.KillAsync(spawned.SubAgentId, "parent-session");
        var updated = await manager.GetAsync(spawned.SubAgentId);

        killed.Should().BeTrue();
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(SubAgentStatus.Killed);
        supervisor.Verify(s => s.StopAsync("parent-agent", spawned.ChildSessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KillAsync_ReturnsFalse_ForUnknownSubAgent()
    {
        var supervisor = new Mock<IAgentSupervisor>(MockBehavior.Strict);
        var manager = CreateScaffoldManager(supervisor.Object);

        var result = await manager.KillAsync("missing-sub-agent", "parent-session");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task KillAsync_ReturnsFalse_WhenNotOwner()
    {
        var childHandle = CreateHangingHandle();
        var supervisor = new Mock<IAgentSupervisor>();
        supervisor
            .Setup(s => s.GetOrCreateAsync("parent-agent", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(childHandle.Object);

        var manager = CreateScaffoldManager(supervisor.Object);
        var spawned = await manager.SpawnAsync(CreateSpawnRequest());

        var result = await manager.KillAsync(spawned.SubAgentId, "other-parent-session");

        result.Should().BeFalse();
        supervisor.Verify(s => s.StopAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnCompletedAsync_UpdatesStatus_AndDelivers()
    {
        var childHandle = CreateHandle();
        var parentHandle = CreateHandle();
        var supervisor = new Mock<IAgentSupervisor>();
        supervisor
            .Setup(s => s.GetOrCreateAsync("parent-agent", It.Is<string>(id => id.StartsWith("parent-session::sub::", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(childHandle.Object);
        supervisor
            .Setup(s => s.GetOrCreateAsync("parent-agent", "parent-session", It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentHandle.Object);

        var manager = CreateScaffoldManager(supervisor.Object);
        var spawned = await manager.SpawnAsync(CreateSpawnRequest());

        await manager.OnCompletedAsync(spawned.SubAgentId, "all done");
        var updated = await manager.GetAsync(spawned.SubAgentId);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(SubAgentStatus.Completed);
        updated.CompletedAt.Should().NotBeNull();
        updated.ResultSummary.Should().Be("all done");
        parentHandle.Verify(
            handle => handle.FollowUpAsync(
                It.Is<string>(message => message.Contains(spawned.SubAgentId, StringComparison.Ordinal) &&
                                          message.Contains("all done", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SpawnAsync_TimesOut_WhenExceedingTimeout()
    {
        var hangingHandle = CreateHangingHandle();
        var supervisor = new Mock<IAgentSupervisor>();
        supervisor
            .Setup(s => s.GetOrCreateAsync("parent-agent", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hangingHandle.Object);

        var manager = CreateScaffoldManager(supervisor.Object);
        var spawned = await manager.SpawnAsync(CreateSpawnRequest(timeoutSeconds: 1));

        await WaitUntilAsync(async () =>
        {
            var info = await manager.GetAsync(spawned.SubAgentId);
            return info?.Status == SubAgentStatus.TimedOut;
        }, TimeSpan.FromSeconds(3));

        var updated = await manager.GetAsync(spawned.SubAgentId);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(SubAgentStatus.TimedOut);
        updated.CompletedAt.Should().NotBeNull();
    }

    private static SubAgentSpawnRequest CreateSpawnRequest(
        string parentAgentId = "parent-agent",
        string parentSessionId = "parent-session",
        int timeoutSeconds = 600)
        => new()
        {
            ParentAgentId = parentAgentId,
            ParentSessionId = parentSessionId,
            Task = "Investigate timeout",
            TimeoutSeconds = timeoutSeconds
        };

    private static Mock<IAgentHandle> CreateHandle()
    {
        var handle = new Mock<IAgentHandle>();
        handle.SetupGet(h => h.AgentId).Returns("parent-agent");
        handle.SetupGet(h => h.SessionId).Returns("session");
        handle.SetupGet(h => h.IsRunning).Returns(false);
        handle.Setup(h => h.PromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse { Content = "done" });
        handle.Setup(h => h.FollowUpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return handle;
    }

    private static Mock<IAgentHandle> CreateHangingHandle()
    {
        var handle = new Mock<IAgentHandle>();
        handle.SetupGet(h => h.AgentId).Returns("parent-agent");
        handle.SetupGet(h => h.SessionId).Returns("session");
        handle.SetupGet(h => h.IsRunning).Returns(true);
        handle.Setup(h => h.PromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new AgentResponse { Content = "never" };
            });
        handle.Setup(h => h.FollowUpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return handle;
    }

    private static ISubAgentManager CreateScaffoldManager(IAgentSupervisor supervisor, SubAgentOptions? options = null)
        => new InterfaceBackedSubAgentManager(supervisor, options ?? new SubAgentOptions());

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Condition was not met before timeout.");
    }

    private sealed class InterfaceBackedSubAgentManager : ISubAgentManager
    {
        private readonly IAgentSupervisor supervisor;
        private readonly SubAgentOptions options;
        private readonly ConcurrentDictionary<string, RuntimeEntry> entries = new(StringComparer.Ordinal);

        public InterfaceBackedSubAgentManager(IAgentSupervisor supervisor, SubAgentOptions options)
        {
            this.supervisor = supervisor;
            this.options = options;
        }

        public async Task<SubAgentInfo> SpawnAsync(SubAgentSpawnRequest request, CancellationToken ct = default)
        {
            var runningCount = entries.Values.Count(entry =>
                entry.Info.ParentSessionId.Equals(request.ParentSessionId, StringComparison.Ordinal) &&
                entry.Info.Status == SubAgentStatus.Running);

            if (runningCount >= options.MaxConcurrentPerSession)
            {
                throw new InvalidOperationException("Maximum concurrent sub-agent limit exceeded.");
            }

            var subAgentId = Guid.NewGuid().ToString("N");
            var childSessionId = $"{request.ParentSessionId}::sub::{subAgentId}";
            var handle = await supervisor.GetOrCreateAsync(request.ParentAgentId, childSessionId, ct);
            var startedAt = DateTimeOffset.UtcNow;
            var timeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : options.DefaultTimeoutSeconds;

            var info = new SubAgentInfo
            {
                SubAgentId = subAgentId,
                ParentSessionId = request.ParentSessionId,
                ChildSessionId = childSessionId,
                Name = request.Name,
                Task = request.Task,
                Model = request.ModelOverride ?? options.DefaultModel,
                Status = SubAgentStatus.Running,
                StartedAt = startedAt
            };

            var lifetimeCts = new CancellationTokenSource();
            var runtime = new RuntimeEntry(request.ParentAgentId, info, handle, lifetimeCts);
            entries[subAgentId] = runtime;

            _ = MonitorRunAsync(runtime, request.Task, timeoutSeconds);
            return info;
        }

        public Task<IReadOnlyList<SubAgentInfo>> ListAsync(string parentSessionId, CancellationToken ct = default)
        {
            var results = entries.Values
                .Select(entry => entry.Info)
                .Where(info => info.ParentSessionId.Equals(parentSessionId, StringComparison.Ordinal))
                .OrderBy(info => info.StartedAt)
                .ToArray();

            return Task.FromResult<IReadOnlyList<SubAgentInfo>>(results);
        }

        public Task<SubAgentInfo?> GetAsync(string subAgentId, CancellationToken ct = default)
        {
            entries.TryGetValue(subAgentId, out var runtime);
            return Task.FromResult(runtime?.Info);
        }

        public async Task<bool> KillAsync(string subAgentId, string requestingSessionId, CancellationToken ct = default)
        {
            if (!entries.TryGetValue(subAgentId, out var runtime))
            {
                return false;
            }

            if (!runtime.Info.ParentSessionId.Equals(requestingSessionId, StringComparison.Ordinal))
            {
                return false;
            }

            runtime.LifetimeCts.Cancel();
            await supervisor.StopAsync(runtime.ParentAgentId, runtime.Info.ChildSessionId, ct);
            UpdateInfo(subAgentId, current => current with
            {
                Status = SubAgentStatus.Killed,
                CompletedAt = DateTimeOffset.UtcNow
            });

            return true;
        }

        public async Task OnCompletedAsync(string subAgentId, string resultSummary, CancellationToken ct = default)
        {
            if (!entries.TryGetValue(subAgentId, out var runtime))
            {
                return;
            }

            UpdateInfo(subAgentId, current => current with
            {
                Status = SubAgentStatus.Completed,
                CompletedAt = DateTimeOffset.UtcNow,
                ResultSummary = resultSummary
            });

            var parentHandle = await supervisor.GetOrCreateAsync(runtime.ParentAgentId, runtime.Info.ParentSessionId, ct);
            await parentHandle.FollowUpAsync($"Sub-agent {subAgentId} completed: {resultSummary}", ct);
        }

        private async Task MonitorRunAsync(RuntimeEntry runtime, string task, int timeoutSeconds)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, runtime.LifetimeCts.Token);

            try
            {
                var response = await runtime.Handle.PromptAsync(task, combinedCts.Token);
                await OnCompletedAsync(runtime.Info.SubAgentId, response.Content);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                UpdateInfo(runtime.Info.SubAgentId, current => current with
                {
                    Status = SubAgentStatus.TimedOut,
                    CompletedAt = DateTimeOffset.UtcNow
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                UpdateInfo(runtime.Info.SubAgentId, current => current with
                {
                    Status = SubAgentStatus.Failed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ResultSummary = ex.Message
                });
            }
        }

        private void UpdateInfo(string subAgentId, Func<SubAgentInfo, SubAgentInfo> mutator)
        {
            entries.AddOrUpdate(
                subAgentId,
                _ => throw new KeyNotFoundException(subAgentId),
                (_, runtime) => runtime with { Info = mutator(runtime.Info) });
        }

        private sealed record RuntimeEntry(
            string ParentAgentId,
            SubAgentInfo Info,
            IAgentHandle Handle,
            CancellationTokenSource LifetimeCts);
    }
}
