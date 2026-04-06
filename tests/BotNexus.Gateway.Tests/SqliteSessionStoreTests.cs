using BotNexus.Gateway.Abstractions.Models;
using BotNexus.Gateway.Sessions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotNexus.Gateway.Tests;

public sealed class SqliteSessionStoreTests
{
    [Fact]
    public async Task GetOrCreateAsync_WithUnknownSession_CreatesAndPersistsSession()
    {
        using var fixture = new StoreFixture();
        var store = fixture.CreateStore();

        var session = await store.GetOrCreateAsync("s1", "agent-a");
        await store.SaveAsync(session);
        var reloaded = await fixture.CreateStore().GetAsync("s1");

        reloaded.Should().NotBeNull();
        reloaded!.SessionId.Should().Be("s1");
        reloaded.AgentId.Should().Be("agent-a");
    }

    [Fact]
    public async Task SaveAsync_WithHistoryAndMetadata_PersistsValues()
    {
        using var fixture = new StoreFixture();
        var store = fixture.CreateStore();
        var session = await store.GetOrCreateAsync("s1", "agent-a");
        session.Metadata["tenant"] = "a";
        session.History.Add(new SessionEntry { Role = "user", Content = "hello" });

        await store.SaveAsync(session);
        var reloaded = await fixture.CreateStore().GetAsync("s1");

        reloaded.Should().NotBeNull();
        reloaded!.History.Should().ContainSingle(e => e.Content == "hello");
        reloaded.Metadata.Should().ContainKey("tenant");
    }

    [Fact]
    public async Task DeleteAsync_WithExistingSession_RemovesSession()
    {
        using var fixture = new StoreFixture();
        var store = fixture.CreateStore();
        var session = await store.GetOrCreateAsync("s1", "agent-a");
        await store.SaveAsync(session);

        await store.DeleteAsync("s1");

        (await fixture.CreateStore().GetAsync("s1")).Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_WithAndWithoutFilter_ReturnsExpectedSessions()
    {
        using var fixture = new StoreFixture();
        var store = fixture.CreateStore();
        await CreateAndSaveAsync(store, "s1", "agent-a");
        await CreateAndSaveAsync(store, "s2", "agent-b");
        await CreateAndSaveAsync(store, "s3", "agent-a");

        var allSessions = await store.ListAsync();
        var filtered = await store.ListAsync("agent-a");

        allSessions.Should().HaveCount(3);
        filtered.Should().OnlyContain(s => s.AgentId == "agent-a");
    }

    private static async Task CreateAndSaveAsync(SqliteSessionStore store, string sessionId, string agentId)
    {
        var session = await store.GetOrCreateAsync(sessionId, agentId);
        await store.SaveAsync(session);
    }

    private sealed class StoreFixture : IDisposable
    {
        public StoreFixture()
        {
            DirectoryPath = Path.Combine(
                AppContext.BaseDirectory,
                "SqliteSessionStoreTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            DatabasePath = Path.Combine(DirectoryPath, "sessions.db");
            ConnectionString = $"Data Source={DatabasePath};Pooling=False";
        }

        public string DirectoryPath { get; }
        public string DatabasePath { get; }
        public string ConnectionString { get; }

        public SqliteSessionStore CreateStore()
            => new(ConnectionString, NullLogger<SqliteSessionStore>.Instance);

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
