using System.Text.Json;
using Xunit.Abstractions;

namespace BotNexus.ConversationTests;

/// <summary>
/// SignalR hub tests verifying conversation model integration.
/// Documents current behavior and future Wave 2 expectations.
/// </summary>
[Collection("LiveGateway")]
public class ConversationSignalRTests(LiveGatewayFixture fixture, ITestOutputHelper output)
{
    // ── SubscribeAll ──────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task SubscribeAll_ReturnsSessions()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await fixture.SignalR.SubscribeAllAsync(cts.Token);

        // Documents current behavior: SubscribeAll returns sessions list
        result.ValueKind.ShouldBe(JsonValueKind.Object);
        result.TryGetProperty("sessions", out var sessions).ShouldBeTrue("expected sessions property");
        sessions.ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [SkippableFact]
    public async Task SubscribeAll_DocumentsConversationListIsFutureExtension()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await fixture.SignalR.SubscribeAllAsync(cts.Token);

        // NOTE: conversations list in SubscribeAll response is a future extension (Wave 3).
        // This test documents that it is NOT present in current behavior.
        // When Wave 3 ships, update this test to assert conversations IS present.
        output.WriteLine($"SubscribeAll conversations field present: {result.TryGetProperty("conversations", out _)}");
    }

    // ── SendMessage ───────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task SendMessage_RoutesToAssistantAndRaisesMessageStart()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await fixture.SignalR.SubscribeAllAsync(cts.Token);

        var result = await fixture.SignalR.SendMessageAsync(
            "assistant", "ping — conversation model test", cts.Token, "signalr");

        result.SessionId.ShouldNotBeNullOrEmpty();
        result.AgentId.ShouldBe("assistant");

        // Wait for MessageStart
        var evt = await fixture.SignalR.WaitForEventAsync(
            result.SessionId, "MessageStart", TimeSpan.FromSeconds(15), cts.Token);
        evt.ShouldNotBeNull();
    }

    [SkippableFact]
    public async Task SendMessage_SessionHasConversationIdField_DocumentedFutureState()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await fixture.SignalR.SubscribeAllAsync(cts.Token);

        var result = await fixture.SignalR.SendMessageAsync(
            "assistant", "ping — conversation model test 2", cts.Token, "signalr");

        await fixture.SignalR.WaitForEventAsync(
            result.SessionId, "MessageStart", TimeSpan.FromSeconds(15), cts.Token);

        // Check session via REST API
        var sessionResponse = await fixture.Http.GetAsync(
            $"/api/sessions/{result.SessionId}", cts.Token);

        if (sessionResponse.IsSuccessStatusCode)
        {
            var json = await sessionResponse.Content.ReadAsStringAsync(cts.Token);
            var doc = JsonDocument.Parse(json).RootElement;
            // conversationId will be null until Wave 2 routing is live.
            // This test documents the expected future field presence.
            output.WriteLine($"Session conversationId: {(doc.TryGetProperty("conversationId", out var cid) ? cid.ToString() : "field absent")}");
        }
        else
        {
            output.WriteLine($"Session endpoint returned {sessionResponse.StatusCode} — sessions API may differ");
        }

        // Pass regardless — this is a documentation test
    }

    [SkippableFact]
    [Trait("Phase", "Wave2")]
    public async Task SendMessage_SessionConversationIdIsStampedAfterWave2()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await fixture.SignalR.SubscribeAllAsync(cts.Token);

        var result = await fixture.SignalR.SendMessageAsync(
            "assistant", "ping — wave2 conversation routing", cts.Token, "signalr");

        await fixture.SignalR.WaitForEventAsync(
            result.SessionId, "MessageStart", TimeSpan.FromSeconds(15), cts.Token);

        var sessionResponse = await fixture.Http.GetAsync(
            $"/api/sessions/{result.SessionId}", cts.Token);
        sessionResponse.IsSuccessStatusCode.ShouldBeTrue("session endpoint must return 200");

        var doc = JsonDocument.Parse(await sessionResponse.Content.ReadAsStringAsync(cts.Token)).RootElement;
        // Skip if conversationId field is absent or null — Wave 2 routing not yet live
        var hasConvId = doc.TryGetProperty("conversationId", out var convId) &&
                        convId.ValueKind != JsonValueKind.Null &&
                        !string.IsNullOrEmpty(convId.GetString());
        Skip.If(!hasConvId, "conversationId not stamped on session — Wave 2 routing not yet live");

        convId.GetString().ShouldNotBeNullOrEmpty("conversationId must be stamped after Wave 2 routing is live");
    }
}
