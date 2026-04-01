using System.Reflection;
using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using BotNexus.Heartbeat;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BotNexus.Tests.Unit.Tests;

public sealed class HeartbeatServiceTests
{
    [Fact]
    public async Task RunConsolidationTriggersAsync_EnabledAgent_IsThrottledByInterval()
    {
        var config = BuildConfig(new Dictionary<string, AgentConfig>
        {
            ["farnsworth"] = new() { EnableMemory = true, MemoryConsolidationIntervalHours = 24 }
        });

        var consolidator = new Mock<IMemoryConsolidator>();
        consolidator.Setup(c => c.ConsolidateAsync("farnsworth", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryConsolidationResult(true, 2, 8));

        var workspace = new Mock<IAgentWorkspace>();
        workspace.Setup(w => w.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        workspace.Setup(w => w.FileExists("HEARTBEAT.md")).Returns(true);
        workspace.Setup(w => w.ReadFileAsync("HEARTBEAT.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[daily] consolidate memory");

        var workspaceFactory = new Mock<IAgentWorkspaceFactory>();
        workspaceFactory.Setup(f => f.Create("farnsworth")).Returns(workspace.Object);

        var sut = new HeartbeatService(
            NullLogger<HeartbeatService>.Instance,
            Options.Create(config),
            consolidator.Object,
            workspaceFactory.Object);

        await InvokeRunConsolidationTriggersAsync(sut);
        await InvokeRunConsolidationTriggersAsync(sut);

        consolidator.Verify(c => c.ConsolidateAsync("farnsworth", It.IsAny<CancellationToken>()), Times.Once);
        workspace.Verify(w => w.ReadFileAsync("HEARTBEAT.md", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunConsolidationTriggersAsync_AgentFailure_DoesNotBlockOtherAgents()
    {
        var config = BuildConfig(new Dictionary<string, AgentConfig>
        {
            ["amy"] = new() { EnableMemory = true, MemoryConsolidationIntervalHours = 24 },
            ["bender"] = new() { EnableMemory = true, MemoryConsolidationIntervalHours = 24 }
        });

        var consolidator = new Mock<IMemoryConsolidator>();
        consolidator.Setup(c => c.ConsolidateAsync("amy", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        consolidator.Setup(c => c.ConsolidateAsync("bender", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryConsolidationResult(true, 1, 3));

        var amyWorkspace = new Mock<IAgentWorkspace>();
        amyWorkspace.Setup(w => w.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        amyWorkspace.Setup(w => w.FileExists("HEARTBEAT.md")).Returns(false);

        var benderWorkspace = new Mock<IAgentWorkspace>();
        benderWorkspace.Setup(w => w.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        benderWorkspace.Setup(w => w.FileExists("HEARTBEAT.md")).Returns(false);

        var workspaceFactory = new Mock<IAgentWorkspaceFactory>();
        workspaceFactory.Setup(f => f.Create("amy")).Returns(amyWorkspace.Object);
        workspaceFactory.Setup(f => f.Create("bender")).Returns(benderWorkspace.Object);

        var sut = new HeartbeatService(
            NullLogger<HeartbeatService>.Instance,
            Options.Create(config),
            consolidator.Object,
            workspaceFactory.Object);

        await InvokeRunConsolidationTriggersAsync(sut);

        consolidator.Verify(c => c.ConsolidateAsync("amy", It.IsAny<CancellationToken>()), Times.Once);
        consolidator.Verify(c => c.ConsolidateAsync("bender", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunConsolidationTriggersAsync_DisabledAgents_AreSkipped()
    {
        var config = BuildConfig(new Dictionary<string, AgentConfig>
        {
            ["fry"] = new() { EnableMemory = false, MemoryConsolidationIntervalHours = 24 },
            ["leela"] = new() { EnableMemory = true, MemoryConsolidationIntervalHours = 24 }
        });

        var consolidator = new Mock<IMemoryConsolidator>();
        consolidator.Setup(c => c.ConsolidateAsync("leela", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryConsolidationResult(true, 0, 0));

        var workspace = new Mock<IAgentWorkspace>();
        workspace.Setup(w => w.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        workspace.Setup(w => w.FileExists("HEARTBEAT.md")).Returns(false);

        var workspaceFactory = new Mock<IAgentWorkspaceFactory>();
        workspaceFactory.Setup(f => f.Create("leela")).Returns(workspace.Object);

        var sut = new HeartbeatService(
            NullLogger<HeartbeatService>.Instance,
            Options.Create(config),
            consolidator.Object,
            workspaceFactory.Object);

        await InvokeRunConsolidationTriggersAsync(sut);

        consolidator.Verify(c => c.ConsolidateAsync("leela", It.IsAny<CancellationToken>()), Times.Once);
        consolidator.Verify(c => c.ConsolidateAsync("fry", It.IsAny<CancellationToken>()), Times.Never);
    }

    private static BotNexusConfig BuildConfig(Dictionary<string, AgentConfig> agents)
        => new()
        {
            Agents = new AgentDefaults
            {
                Named = agents
            }
        };

    private static async Task InvokeRunConsolidationTriggersAsync(HeartbeatService service)
    {
        var method = typeof(HeartbeatService).GetMethod("RunConsolidationTriggersAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("HeartbeatService should expose consolidation cycle logic");

        var task = method!.Invoke(service, [CancellationToken.None]) as Task;
        task.Should().NotBeNull();
        await task!;
    }
}
