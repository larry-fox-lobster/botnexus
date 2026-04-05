using BotNexus.Providers.Core.Registry;
using BotNexus.Providers.Core.Models;
using BotNexus.Providers.Core.Streaming;
using FluentAssertions;
using Moq;

namespace BotNexus.Providers.Core.Tests.Registry;

public class ApiProviderRegistryTests : IDisposable
{
    private readonly ApiProviderRegistry _registry = new();

    public ApiProviderRegistryTests()
    {
        _registry.Clear();
    }

    public void Dispose()
    {
        _registry.Clear();
    }

    private static Mock<IApiProvider> CreateMockProvider(string api)
    {
        var mock = new Mock<IApiProvider>();
        mock.Setup(p => p.Api).Returns(api);
        return mock;
    }

    [Fact]
    public void Register_AndRetrieve_ReturnsProvider()
    {
        var mock = CreateMockProvider("test-api");
        _registry.Register(mock.Object);

        var result = _registry.Get("test-api");

        result.Should().BeSameAs(mock.Object);
    }

    [Fact]
    public void Get_UnregisteredApi_ReturnsNull()
    {
        var result = _registry.Get("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public void Register_WithSourceId_UnregisterBySourceId()
    {
        var mock = CreateMockProvider("test-api");
        _registry.Register(mock.Object, "source-1");

        _registry.Unregister("source-1");

        _registry.Get("test-api").Should().BeNull();
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        var mock1 = CreateMockProvider("api-1");
        var mock2 = CreateMockProvider("api-2");
        _registry.Register(mock1.Object);
        _registry.Register(mock2.Object);

        var all = _registry.GetAll();

        all.Should().HaveCount(2);
    }

    [Fact]
    public void Clear_RemovesAllProviders()
    {
        var mock = CreateMockProvider("test-api");
        _registry.Register(mock.Object);

        _registry.Clear();

        _registry.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Register_ReplaceExisting_ForSameApi()
    {
        var mock1 = CreateMockProvider("same-api");
        var mock2 = CreateMockProvider("same-api");
        _registry.Register(mock1.Object);
        _registry.Register(mock2.Object);

        var result = _registry.Get("same-api");

        result.Should().BeSameAs(mock2.Object);
    }
}
