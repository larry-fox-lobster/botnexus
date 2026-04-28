using System.Net;
using System.Text.Json;
using Xunit.Abstractions;

namespace BotNexus.ConversationTests;

/// <summary>
/// Tests for conversation channel binding behavior.
/// These will be live after Wave 2 ships.
/// All are marked [Trait("Phase", "Wave2")] or [Trait("Phase", "Wave3")].
/// </summary>
[Collection("LiveGateway")]
public class ConversationBindingTests(LiveGatewayFixture fixture, ITestOutputHelper output)
{
    // ── Default conversation bindings ─────────────────────────────────────────

    [SkippableFact]
    [Trait("Phase", "Wave2")]
    public async Task DefaultConversation_HasNoBindingsInitially()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");

        // Get the default conversation for assistant
        var listResponse = await fixture.Conversations.GetConversationsAsync("assistant");
        Skip.If(listResponse.StatusCode != HttpStatusCode.OK,
            "Conversations endpoint not yet live (Wave 3)");

        var items = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync())
            .RootElement.EnumerateArray().ToList();
        var defaultConv = items.FirstOrDefault(i =>
            i.TryGetProperty("isDefault", out var v) && v.GetBoolean());
        Skip.If(defaultConv.ValueKind == JsonValueKind.Undefined,
            "No default conversation found — gateway may need to initialise");

        var convId = defaultConv.GetProperty("conversationId").GetString()!;
        var bindingsResponse = await fixture.Conversations.GetConversationBindingsAsync(convId);

        // Document: either 200 with empty array, or 404 if not yet implemented
        output.WriteLine($"Bindings response: {bindingsResponse.StatusCode}");
        if (bindingsResponse.StatusCode == HttpStatusCode.OK)
        {
            var json = await bindingsResponse.Content.ReadAsStringAsync();
            var arr = JsonDocument.Parse(json).RootElement;
            arr.ValueKind.ShouldBe(JsonValueKind.Array);
            arr.GetArrayLength().ShouldBe(0, "default conversation should have no bindings initially");
        }
        else
        {
            bindingsResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound,
                "expected either 200+empty-array or 404 when no bindings");
        }
    }

    // ── Add binding ───────────────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Phase", "Wave2")]
    public async Task AddBinding_Returns201WithBindingId()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        var convId = await GetOrCreateConversationIdAsync();
        Skip.If(convId is null, "Could not obtain conversationId — Wave 3 endpoint not live");

        var response = await fixture.Conversations.AddBindingAsync(
            convId!, "signalr", "test-address", "Single");

        output.WriteLine($"AddBinding response: {response.StatusCode}");
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;
        doc.TryGetProperty("bindingId", out var bindingId).ShouldBeTrue("expected bindingId in response");
        bindingId.GetString().ShouldNotBeNullOrEmpty();
    }

    [SkippableFact]
    [Trait("Phase", "Wave2")]
    public async Task GetConversation_AfterBinding_IncludesBindingInArray()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        var convId = await GetOrCreateConversationIdAsync();
        Skip.If(convId is null, "Could not obtain conversationId — Wave 3 endpoint not live");

        var addResponse = await fixture.Conversations.AddBindingAsync(
            convId!, "signalr", "test-address-get", "Single");
        Skip.If(addResponse.StatusCode != HttpStatusCode.Created,
            "AddBinding not yet live (Wave 2)");

        var bindingId = JsonDocument.Parse(await addResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("bindingId").GetString()!;

        var convResponse = await fixture.Conversations.GetConversationAsync(convId!);
        convResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await convResponse.Content.ReadAsStringAsync()).RootElement;
        doc.TryGetProperty("bindings", out var bindings).ShouldBeTrue("expected bindings array on conversation");
        bindings.EnumerateArray()
            .Any(b => b.TryGetProperty("bindingId", out var bid) && bid.GetString() == bindingId)
            .ShouldBeTrue("new binding should appear in conversation bindings");
    }

    [SkippableFact]
    [Trait("Phase", "Wave2")]
    public async Task DeleteBinding_Returns204()
    {
        Skip.If(!fixture.IsAvailable, "Dev gateway not running at localhost:5006");
        var convId = await GetOrCreateConversationIdAsync();
        Skip.If(convId is null, "Could not obtain conversationId — Wave 3 endpoint not live");

        var addResponse = await fixture.Conversations.AddBindingAsync(
            convId!, "signalr", "test-address-delete", "Single");
        Skip.If(addResponse.StatusCode != HttpStatusCode.Created,
            "AddBinding not yet live (Wave 2)");

        var bindingId = JsonDocument.Parse(await addResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("bindingId").GetString()!;

        var deleteResponse = await fixture.Conversations.DeleteBindingAsync(convId!, bindingId);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string?> GetOrCreateConversationIdAsync()
    {
        var listResponse = await fixture.Conversations.GetConversationsAsync("assistant");
        if (listResponse.StatusCode == HttpStatusCode.OK)
        {
            var items = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync())
                .RootElement.EnumerateArray().ToList();
            if (items.Count > 0)
                return items[0].GetProperty("conversationId").GetString();
        }

        var createResponse = await fixture.Conversations.CreateConversationAsync("assistant", "Binding Test");
        if (createResponse.StatusCode == HttpStatusCode.Created)
            return JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync())
                .RootElement.GetProperty("conversationId").GetString();

        return null;
    }
}
