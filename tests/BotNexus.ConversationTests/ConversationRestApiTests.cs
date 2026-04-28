using System.Net;
using System.Text.Json;
using Xunit.Abstractions;

namespace BotNexus.ConversationTests;

/// <summary>
/// REST API tests for the Conversation endpoints.
/// These skip cleanly if the gateway is not running OR if the endpoints are not yet live (Wave 3).
/// Wave 3 endpoints (Fry) will be built later — tests are written now and skip until live.
/// </summary>
[Collection("LiveGateway")]
public class ConversationRestApiTests(LiveGatewayFixture fixture, ITestOutputHelper output)
{
    // ── GET /api/conversations ────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Phase", "Wave3")]
    public async Task GetConversations_ReturnsOk()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        var response = await fixture.Conversations.GetConversationsAsync("assistant");
        Skip.If(response.StatusCode == HttpStatusCode.NotFound,
            "GET /api/conversations not yet live (Wave 3 — Fry)");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [SkippableFact]
    [Trait("Phase", "Wave3")]
    public async Task GetConversations_ReturnsArray()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        var response = await fixture.Conversations.GetConversationsAsync("assistant");
        Skip.If(response.StatusCode == HttpStatusCode.NotFound,
            "GET /api/conversations not yet live (Wave 3 — Fry)");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [SkippableFact]
    [Trait("Phase", "Wave3")]
    public async Task GetConversations_EachItemHasRequiredFields()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        var response = await fixture.Conversations.GetConversationsAsync("assistant");
        Skip.If(response.StatusCode == HttpStatusCode.NotFound,
            "GET /api/conversations not yet live (Wave 3 — Fry)");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var items = JsonDocument.Parse(json).RootElement;

        foreach (var item in items.EnumerateArray())
        {
            item.TryGetProperty("conversationId", out _).ShouldBeTrue("expected conversationId field");
            item.TryGetProperty("agentId", out _).ShouldBeTrue("expected agentId field");
            item.TryGetProperty("title", out _).ShouldBeTrue("expected title field");
            item.TryGetProperty("isDefault", out _).ShouldBeTrue("expected isDefault field");
            item.TryGetProperty("status", out _).ShouldBeTrue("expected status field");
        }
    }

    [SkippableFact]
    [Trait("Phase", "Wave3")]
    public async Task GetConversations_DefaultConversationExistsForAgent()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        var response = await fixture.Conversations.GetConversationsAsync("assistant");
        Skip.If(response.StatusCode == HttpStatusCode.NotFound,
            "GET /api/conversations not yet live (Wave 3 — Fry)");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var items = JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();
        items.ShouldNotBeEmpty("expected at least one conversation (default)");
        items.Any(i => i.TryGetProperty("isDefault", out var v) && v.GetBoolean())
            .ShouldBeTrue("expected at least one default conversation");
    }

    // ── POST /api/conversations ───────────────────────────────────────────────

    [SkippableFact]
    [Trait("Phase", "Wave3")]
    public async Task CreateConversation_Returns201WithBody()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        var response = await fixture.Conversations.CreateConversationAsync("assistant", "Test Conversation");
        Skip.If(response.StatusCode == HttpStatusCode.NotFound,
            "POST /api/conversations not yet live (Wave 3 — Fry)");
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;
        doc.TryGetProperty("conversationId", out _).ShouldBeTrue("expected conversationId");
        doc.TryGetProperty("agentId", out var aid).ShouldBeTrue("expected agentId");
        aid.GetString().ShouldBe("assistant");
        doc.TryGetProperty("title", out var title).ShouldBeTrue("expected title");
        title.GetString().ShouldBe("Test Conversation");
    }

    // ── GET /api/conversations/{id} ───────────────────────────────────────────

    [SkippableFact]
    [Trait("Phase", "Wave3")]
    public async Task GetConversation_Returns200ForKnownConversation()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        // Try to create first to get an ID
        var create = await fixture.Conversations.CreateConversationAsync("assistant", "Fetch Test");
        Skip.If(create.StatusCode == HttpStatusCode.NotFound,
            "POST /api/conversations not yet live (Wave 3 — Fry)");
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = JsonDocument.Parse(await create.Content.ReadAsStringAsync())
            .RootElement.GetProperty("conversationId").GetString()!;

        var response = await fixture.Conversations.GetConversationAsync(id);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [SkippableFact]
    [Trait("Phase", "Wave3")]
    public async Task GetConversation_Returns404ForUnknownId()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        var response = await fixture.Conversations.GetConversationAsync("00000000-0000-0000-0000-000000000000");
        // If the endpoint itself doesn't exist, skip rather than fail
        Skip.If(response.StatusCode == HttpStatusCode.MethodNotAllowed,
            "GET /api/conversations/{id} not yet live (Wave 3 — Fry)");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── GET /api/conversations/{id}/history ───────────────────────────────────

    [SkippableFact]
    [Trait("Phase", "Wave3")]
    public async Task GetConversationHistory_Returns200WithEntriesArray()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        var create = await fixture.Conversations.CreateConversationAsync("assistant", "History Test");
        Skip.If(create.StatusCode == HttpStatusCode.NotFound,
            "POST /api/conversations not yet live (Wave 3 — Fry)");
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = JsonDocument.Parse(await create.Content.ReadAsStringAsync())
            .RootElement.GetProperty("conversationId").GetString()!;

        var response = await fixture.Conversations.GetConversationHistoryAsync(id);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;
        doc.TryGetProperty("entries", out var entries).ShouldBeTrue("expected entries array");
        entries.ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [SkippableFact]
    [Trait("Phase", "Wave3")]
    public async Task GetConversationHistory_BoundaryEntriesHaveRequiredFields()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        var create = await fixture.Conversations.CreateConversationAsync("assistant", "Boundary Test");
        Skip.If(create.StatusCode == HttpStatusCode.NotFound,
            "POST /api/conversations not yet live (Wave 3 — Fry)");
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = JsonDocument.Parse(await create.Content.ReadAsStringAsync())
            .RootElement.GetProperty("conversationId").GetString()!;

        var response = await fixture.Conversations.GetConversationHistoryAsync(id);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var entries = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("entries").EnumerateArray();

        foreach (var entry in entries.Where(e =>
            e.TryGetProperty("kind", out var k) && k.GetString() == "boundary"))
        {
            entry.TryGetProperty("sessionId", out _).ShouldBeTrue("boundary entry needs sessionId");
            entry.TryGetProperty("timestamp", out _).ShouldBeTrue("boundary entry needs timestamp");
        }
    }
}
