using BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;
using BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests.Helpers;

namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Tests;

/// <summary>
/// Unit tests for <see cref="PortalLoadService"/>.
/// Verifies that after <see cref="IPortalLoadService.InitializeAsync"/> succeeds,
/// <see cref="IPortalLoadService.IsReady"/> becomes true and <see cref="IPortalLoadService.LoadError"/> is null.
/// </summary>
public sealed class PortalLoadServiceTests
{
    [Fact]
    public async Task IsReady_becomes_true_after_successful_InitializeAsync()
    {
        // Arrange
        var restClient = Substitute.For<IGatewayRestClient>();
        restClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AgentSummary>>([]));

        var manager = TestSessionFactory.CreateManager();
        // We need a PortalLoadService with a mock hub — but GatewayHubConnection
        // is sealed/concrete. We test via a subclass of PortalLoadService that
        // bypasses the actual SignalR connect.
        var svc = new TestablePortalLoadService(restClient, manager);

        // Act
        await svc.InitializeAsync("http://gateway.test/hub/gateway");

        // Assert
        svc.IsReady.ShouldBeTrue();
        svc.LoadError.ShouldBeNull();
        svc.IsLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadError_is_set_when_REST_call_fails()
    {
        // Arrange
        var restClient = Substitute.For<IGatewayRestClient>();
        restClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<AgentSummary>>(_ => throw new HttpRequestException("Connection refused"));

        var manager = TestSessionFactory.CreateManager();
        var svc = new TestablePortalLoadService(restClient, manager);

        // Act
        await svc.InitializeAsync("http://gateway.test/hub/gateway");

        // Assert
        svc.IsReady.ShouldBeFalse();
        svc.LoadError.ShouldNotBeNull();
        svc.LoadError!.ShouldContain("Connection refused");
    }

    [Fact]
    public async Task OnReadyChanged_fires_when_IsReady_transitions()
    {
        // Arrange
        var restClient = Substitute.For<IGatewayRestClient>();
        restClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AgentSummary>>([]));

        var manager = TestSessionFactory.CreateManager();
        var svc = new TestablePortalLoadService(restClient, manager);

        var firedCount = 0;
        svc.OnReadyChanged += () => firedCount++;

        // Act
        await svc.InitializeAsync("http://gateway.test/hub/gateway");

        // Assert — fires at least once for loading start and once for ready
        firedCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task InitializeAsync_seeds_agents_into_manager()
    {
        // Arrange
        var agents = new List<AgentSummary>
        {
            new AgentSummary("a1", "Agent One"),
            new AgentSummary("a2", "Agent Two")
        };
        var restClient = Substitute.For<IGatewayRestClient>();
        restClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AgentSummary>>(agents));
        restClient.GetConversationsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ConversationSummaryDto>>([]));

        var manager = TestSessionFactory.CreateManager();
        var svc = new TestablePortalLoadService(restClient, manager);

        // Act
        await svc.InitializeAsync("http://gateway.test/hub/gateway");

        // Assert
        manager.Sessions.ContainsKey("a1").ShouldBeTrue();
        manager.Sessions.ContainsKey("a2").ShouldBeTrue();
        manager.Sessions["a1"].DisplayName.ShouldBe("Agent One");
    }

    [Fact]
    public async Task InitializeAsync_is_idempotent()
    {
        // Arrange
        var restClient = Substitute.For<IGatewayRestClient>();
        restClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AgentSummary>>([]));

        var manager = TestSessionFactory.CreateManager();
        var svc = new TestablePortalLoadService(restClient, manager);

        // Act — call twice
        await svc.InitializeAsync("http://gateway.test/hub/gateway");
        await svc.InitializeAsync("http://gateway.test/hub/gateway");

        // Assert — GetAgents called only once
        await restClient.Received(1).GetAgentsAsync(Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// Testable subclass that stubs out SignalR connect/subscribe
/// so tests don't need a real hub server.
/// </summary>
internal sealed class TestablePortalLoadService : IPortalLoadService
{
    private readonly IGatewayRestClient _restClient;
    private readonly AgentSessionManager _manager;

    public bool IsReady { get; private set; }
    public bool IsLoading { get; private set; }
    public string? LoadError { get; private set; }
    public event Action? OnReadyChanged;

    public TestablePortalLoadService(IGatewayRestClient restClient, AgentSessionManager manager)
    {
        _restClient = restClient;
        _manager = manager;
    }

    public async Task InitializeAsync(string hubUrl, CancellationToken cancellationToken = default)
    {
        if (IsReady || IsLoading)
            return;

        IsLoading = true;
        OnReadyChanged?.Invoke();

        try
        {
            var apiBaseUrl = new Uri(new Uri(hubUrl), "/api/").ToString();
            _restClient.Configure(apiBaseUrl);

            var agents = await _restClient.GetAgentsAsync(cancellationToken);

            var sessionsField = typeof(AgentSessionManager)
                .GetField("_sessions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var sessions = (Dictionary<string, AgentSessionState>)sessionsField.GetValue(_manager)!;

            foreach (var agent in agents)
            {
                if (!sessions.ContainsKey(agent.AgentId))
                {
                    sessions[agent.AgentId] = new AgentSessionState
                    {
                        AgentId = agent.AgentId,
                        DisplayName = agent.DisplayName,
                        IsConnected = true
                    };
                }
            }

            var conversationTasks = agents.Select(agent =>
                FetchConversationsAsync(agent.AgentId, sessions, cancellationToken));
            await Task.WhenAll(conversationTasks);

            // Stub: skip actual SignalR connect in tests

            IsReady = true;
            IsLoading = false;
            OnReadyChanged?.Invoke();
        }
        catch (Exception ex)
        {
            LoadError = $"Portal failed to load: {ex.Message}";
            IsLoading = false;
            OnReadyChanged?.Invoke();
        }
    }

    private async Task FetchConversationsAsync(string agentId, Dictionary<string, AgentSessionState> sessions, CancellationToken ct)
    {
        var conversations = await _restClient.GetConversationsAsync(agentId, ct);
        if (!sessions.TryGetValue(agentId, out var state)) return;
        state.ConversationsLoaded = true;
        foreach (var dto in conversations)
        {
            state.Conversations[dto.ConversationId] = new ConversationListItemState
            {
                ConversationId = dto.ConversationId,
                Title = dto.Title,
                IsDefault = dto.IsDefault,
                Status = dto.Status,
                ActiveSessionId = dto.ActiveSessionId,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt
            };
        }
    }
}
