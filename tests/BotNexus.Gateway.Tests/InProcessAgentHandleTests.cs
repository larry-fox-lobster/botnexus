using BotNexus.AgentCore;
using BotNexus.AgentCore.Configuration;
using BotNexus.AgentCore.Types;
using BotNexus.Gateway.Isolation;
using BotNexus.Providers.Core;
using BotNexus.Providers.Core.Models;
using BotNexus.Providers.Core.Registry;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotNexus.Gateway.Tests;

public sealed class InProcessAgentHandleTests
{
    [Fact]
    public async Task SteerAsync_WhenCalled_QueuesSteeringMessage()
    {
        var (agent, handle) = CreateHandle();

        await handle.SteerAsync("adjust behavior");

        agent.HasQueuedMessages.Should().BeTrue();
    }

    [Fact]
    public async Task FollowUpAsync_WhenCalled_QueuesFollowUpMessage()
    {
        var (agent, handle) = CreateHandle();

        await handle.FollowUpAsync("do this next");

        agent.HasQueuedMessages.Should().BeTrue();
    }

    [Fact]
    public async Task SteerAsync_WhenAgentIsNotRunning_DoesNotThrow()
    {
        var (agent, handle) = CreateHandle();
        agent.Status.Should().Be(AgentStatus.Idle);

        var act = async () => await handle.SteerAsync("non-blocking steer");

        await act.Should().NotThrowAsync();
        agent.HasQueuedMessages.Should().BeTrue();
    }

    private static (Agent Agent, InProcessAgentHandle Handle) CreateHandle()
    {
        var modelRegistry = new ModelRegistry();
        modelRegistry.Register("test-provider", new LlmModel(
            Id: "test-model",
            Name: "test-model",
            Api: "test-api",
            Provider: "test-provider",
            BaseUrl: "http://localhost",
            Reasoning: false,
            Input: ["text"],
            Cost: new ModelCost(0, 0, 0, 0),
            ContextWindow: 8192,
            MaxTokens: 1024));

        var llmClient = new LlmClient(new ApiProviderRegistry(), modelRegistry);
        var model = modelRegistry.GetModel("test-provider", "test-model")!;
        var options = new AgentOptions(
            InitialState: new AgentInitialState(SystemPrompt: "test", Model: model),
            Model: model,
            LlmClient: llmClient,
            ConvertToLlm: null,
            TransformContext: null,
            GetApiKey: (_, _) => Task.FromResult<string?>(null),
            GetSteeringMessages: null,
            GetFollowUpMessages: null,
            ToolExecutionMode: ToolExecutionMode.Parallel,
            BeforeToolCall: null,
            AfterToolCall: null,
            GenerationSettings: new SimpleStreamOptions(),
            SteeringMode: QueueMode.All,
            FollowUpMode: QueueMode.All,
            SessionId: "session-1");

        var agent = new Agent(options);
        var handle = new InProcessAgentHandle(agent, "agent-a", "session-1", NullLogger.Instance);
        return (agent, handle);
    }
}
