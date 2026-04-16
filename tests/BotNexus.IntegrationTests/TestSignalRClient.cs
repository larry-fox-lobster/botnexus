using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace BotNexus.IntegrationTests;

public class TestSignalRClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ConcurrentDictionary<string, List<ReceivedEvent>> _events = new();

    public TestSignalRClient(string baseUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hub/gateway")
            .Build();

        RegisterEventHandlers();
    }

    private void RegisterEventHandlers()
    {
        foreach (var method in new[] { "MessageStart", "ContentDelta", "ThinkingDelta",
                                       "ToolStart", "ToolEnd", "MessageEnd", "Error",
                                       "Connected", "SessionReset" })
        {
            _connection.On<JsonElement>(method, payload =>
            {
                // Extract sessionId — handle both string and {value: "..."} shapes
                string sessionId = "unknown";
                if (payload.TryGetProperty("sessionId", out var sid))
                {
                    if (sid.ValueKind == JsonValueKind.String)
                        sessionId = sid.GetString() ?? "unknown";
                    else if (sid.ValueKind == JsonValueKind.Object && sid.TryGetProperty("value", out var val))
                        sessionId = val.GetString() ?? "unknown";
                }

                var contentDelta = payload.TryGetProperty("contentDelta", out var cd)
                    ? cd.GetString() : null;

                var evt = new ReceivedEvent(method, DateTimeOffset.UtcNow, payload, contentDelta);
                var eventList = _events.GetOrAdd(sessionId, _ => []);
                lock (eventList) { eventList.Add(evt); }

                // Debug: show raw sessionId shape for first event
                if (_events.Values.Sum(l => { lock (l) { return l.Count; } }) <= 3)
                    Console.WriteLine($"    [DEBUG] Raw sessionId JSON: {(payload.TryGetProperty("sessionId", out var dbg) ? dbg.ToString() : "missing")}");

                Console.WriteLine($"    📨 [{method}] sid={sessionId} {(contentDelta is not null ? $"delta=\"{contentDelta[..Math.Min(50, contentDelta.Length)]}\"" : "")}");
            });
        }
    }

    public Task ConnectAsync(CancellationToken ct)
        => _connection.StartAsync(ct);

    public Task<JsonElement> SubscribeAllAsync(CancellationToken ct)
        => _connection.InvokeAsync<JsonElement>("SubscribeAll", ct);

    public async Task<string> SendMessageAsync(string agentId, string content, CancellationToken ct)
    {
        var result = await _connection.InvokeAsync<JsonElement>("SendMessage", agentId, "signalr", content, ct);
        var sessionId = result.GetProperty("sessionId").GetString() ?? throw new Exception("No sessionId returned");
        Console.WriteLine($"    [DEBUG] SendMessage returned sessionId: '{sessionId}'");
        return sessionId;
    }

    public Task ResetSessionAsync(string agentId, string sessionId, CancellationToken ct)
        => _connection.InvokeAsync("ResetSession", agentId, sessionId, ct);

    public IReadOnlyList<ReceivedEvent> GetEvents(string sessionId)
    {
        if (!_events.TryGetValue(sessionId, out var list)) return [];
        lock (list) { return list.ToList().AsReadOnly(); }
    }

    public async Task<ReceivedEvent> WaitForEventAsync(string sessionId, string eventType,
        TimeSpan timeout, CancellationToken ct)
    {
        Console.WriteLine($"    [DEBUG] WaitForEvent: looking for '{eventType}' on sid='{sessionId}'");
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var events = GetEvents(sessionId);
            var match = events.FirstOrDefault(e => e.Method == eventType);
            if (match is not null)
                return match;
            await Task.Delay(100, ct);
        }

        // Dump debug info on timeout
        Console.WriteLine($"    ⚠️  Timeout waiting for {eventType} on session {sessionId[..Math.Min(12, sessionId.Length)]}...");
        Console.WriteLine($"    Events for session: {GetEvents(sessionId).Count}");
        foreach (var e in GetEvents(sessionId))
            Console.WriteLine($"      [{e.Method}] at {e.ReceivedAt:HH:mm:ss.fff}");
        Console.WriteLine($"    All sessions: {string.Join(", ", _events.Keys.Select(k => k[..Math.Min(8, k.Length)]))}");

        throw new TimeoutException($"Timed out waiting for {eventType} on session {sessionId[..Math.Min(12, sessionId.Length)]}...");
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}

public record ReceivedEvent(string Method, DateTimeOffset ReceivedAt, JsonElement Payload, string? ContentDelta);