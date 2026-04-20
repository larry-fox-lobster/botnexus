using BotNexus.Gateway.Configuration;
using Microsoft.Extensions.Options;
using System.IO.Abstractions;

namespace BotNexus.Gateway.Tests;

public sealed class PlatformConfigWatcherTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _configPath;

    public PlatformConfigWatcherTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "botnexus-platform-watch-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _configPath = Path.Combine(_rootPath, "config.json");
        File.WriteAllText(_configPath, """{"defaultAgentId":"agent-a"}""");
    }

    [Fact]
    public async Task Watch_WhenConfigChanges_InvokesCallback()
    {
        var callback = new TaskCompletionSource<PlatformConfig>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = PlatformConfigLoader.Watch(_configPath, config => callback.TrySetResult(config), fileSystem: new FileSystem());

        File.WriteAllText(_configPath, """{"defaultAgentId":"agent-b"}""");

        var completed = await Task.WhenAny(callback.Task, Task.Delay(TimeSpan.FromSeconds(10)));

        completed.ShouldBe(callback.Task);
        (await callback.Task).Gateway?.DefaultAgentId.ShouldBe("agent-b");
    }

    [Fact]
    public async Task Watch_WhenConfigChanges_RaisesConfigChangedEvent()
    {
        var callback = new TaskCompletionSource<PlatformConfig>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<PlatformConfig> handler = config => callback.TrySetResult(config);
        PlatformConfigLoader.ConfigChanged += handler;

        try
        {
            using var watcher = PlatformConfigLoader.Watch(_configPath, fileSystem: new FileSystem());
            File.WriteAllText(_configPath, """{"defaultAgentId":"agent-c"}""");

            var completed = await Task.WhenAny(callback.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            completed.ShouldBe(callback.Task);
            (await callback.Task).Gateway?.DefaultAgentId.ShouldBe("agent-c");
        }
        finally
        {
            PlatformConfigLoader.ConfigChanged -= handler;
        }
    }

    [Fact]
    public async Task Watch_WhenConfigBecomesInvalid_InvokesErrorCallback()
    {
        var callback = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = PlatformConfigLoader.Watch(_configPath, onError: ex => callback.TrySetResult(ex), fileSystem: new FileSystem());

        File.WriteAllText(_configPath, "{ invalid json");

        var completed = await Task.WhenAny(callback.Task, Task.Delay(TimeSpan.FromSeconds(10)));

        completed.ShouldBe(callback.Task);
        (await callback.Task).ShouldBeOfType<OptionsValidationException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
