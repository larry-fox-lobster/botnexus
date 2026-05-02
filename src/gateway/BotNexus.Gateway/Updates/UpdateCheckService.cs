using Microsoft.Extensions.Hosting;

namespace BotNexus.Gateway.Updates;

/// <summary>
/// STUB — implementation pending by Farnsworth (Wave 1).
/// Exists only so test projects compile; all methods throw <see cref="NotImplementedException"/>.
/// </summary>
public sealed class UpdateCheckService : IUpdateCheckService, IHostedService
{
    public UpdateCheckService(
        Microsoft.Extensions.Options.IOptions<Configuration.PlatformConfig> config,
        System.Net.Http.HttpClient httpClient,
        System.IO.Abstractions.IFileSystem fileSystem,
        Microsoft.Extensions.Logging.ILogger<UpdateCheckService> logger,
        IHostApplicationLifetime lifetime)
    {
        throw new NotImplementedException("UpdateCheckService not yet implemented.");
    }

    /// <inheritdoc/>
    public UpdateStatusResult GetCurrentStatus() =>
        throw new NotImplementedException("UpdateCheckService not yet implemented.");

    /// <inheritdoc/>
    public Task<UpdateStatusResult> CheckNowAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("UpdateCheckService not yet implemented.");

    /// <inheritdoc/>
    public Task<UpdateStartResult> StartUpdateAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("UpdateCheckService not yet implemented.");

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException("UpdateCheckService not yet implemented.");

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException("UpdateCheckService not yet implemented.");
}
