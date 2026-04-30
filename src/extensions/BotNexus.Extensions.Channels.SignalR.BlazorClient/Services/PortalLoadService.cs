namespace BotNexus.Extensions.Channels.SignalR.BlazorClient.Services;

/// <summary>
/// Owns the portal startup sequence: REST-first, SignalR-second.
/// Sets <see cref="IsReady"/> once the initial data load and SignalR connection succeed.
/// </summary>
public sealed class PortalLoadService : IPortalLoadService
{
    private readonly IGatewayRestClient _restClient;
    private readonly GatewayHubConnection _hub;
    private readonly AgentSessionManager _manager;

    /// <inheritdoc />
    public bool IsReady { get; private set; }

    /// <inheritdoc />
    public bool IsLoading { get; private set; }

    /// <inheritdoc />
    public string? LoadError { get; private set; }

    /// <inheritdoc />
    public event Action? OnReadyChanged;

    public PortalLoadService(
        IGatewayRestClient restClient,
        GatewayHubConnection hub,
        AgentSessionManager manager)
    {
        _restClient = restClient;
        _hub = hub;
        _manager = manager;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(string hubUrl, CancellationToken cancellationToken = default)
    {
        // Idempotent — skip if already ready or in progress
        if (IsReady || IsLoading)
            return;

        IsLoading = true;
        LoadError = null;
        OnReadyChanged?.Invoke();

        try
        {
            // Step 1: derive API base URL from hub URL
            var apiBaseUrl = new Uri(new Uri(hubUrl), "/api/").ToString();
            _restClient.Configure(apiBaseUrl);

            // Step 2: GET /api/agents
            var agents = await _restClient.GetAgentsAsync(cancellationToken);

            // Step 3: seed agent state in AgentSessionManager via internal _sessions field
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
                else
                {
                    sessions[agent.AgentId].DisplayName = agent.DisplayName;
                }
            }

            // Step 4: GET /api/conversations for each agent in parallel
            var conversationTasks = agents.Select(agent =>
                FetchAndSeedConversationsAsync(agent.AgentId, sessions, cancellationToken));
            await Task.WhenAll(conversationTasks);

            // Step 5: Connect SignalR
            await _hub.ConnectAsync(hubUrl);

            // Step 6: SubscribeAll — register live sessions
            var subscribeResult = await _hub.SubscribeAllAsync();
            foreach (var session in subscribeResult.Sessions)
                _manager.RegisterSession(session.AgentId, session.SessionId, session.ChannelType);

            // Step 7: set _apiBaseUrl on the manager so existing code (ConversationHistory etc.) still works
            var apiBaseUrlField = typeof(AgentSessionManager)
                .GetField("_apiBaseUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            apiBaseUrlField.SetValue(_manager, apiBaseUrl);

            // Step 8: Complete
            IsReady = true;
            IsLoading = false;
            OnReadyChanged?.Invoke();
        }
        catch (Exception ex)
        {
            LoadError = $"Portal failed to load: {ex.Message}";
            IsLoading = false;
            OnReadyChanged?.Invoke();
            Console.Error.WriteLine($"PortalLoadService.InitializeAsync failed: {ex}");
        }
    }

    private async Task FetchAndSeedConversationsAsync(
        string agentId,
        Dictionary<string, AgentSessionState> sessions,
        CancellationToken ct)
    {
        try
        {
            var conversations = await _restClient.GetConversationsAsync(agentId, ct);
            if (!sessions.TryGetValue(agentId, out var state))
                return;

            state.Conversations.Clear();
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
                state.ConversationMessageStores.TryAdd(dto.ConversationId, new List<ChatMessage>());
            }

            // Auto-select default conversation
            if (state.ActiveConversationId is null && state.Conversations.Count > 0)
            {
                var defaultConv = state.Conversations.Values.FirstOrDefault(c => c.IsDefault)
                    ?? state.Conversations.Values.OrderByDescending(c => c.UpdatedAt).First();
                state.ActiveConversationId = defaultConv.ConversationId;
                if (defaultConv.ActiveSessionId is not null)
                    state.SessionId = defaultConv.ActiveSessionId;
            }

            state.ConversationsLoaded = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PortalLoadService: failed to load conversations for {agentId}: {ex.Message}");
        }
    }
}
