using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using BotNexus.Cron.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotNexus.Cron;

public sealed class CronJobFactory(
    IOptions<CronConfig> options,
    IServiceProvider serviceProvider,
    ILogger<CronJobFactory> logger)
{
    private readonly CronConfig _config = options?.Value ?? new CronConfig();
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<CronJobFactory> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public void CreateAndRegisterAll(ICronService cronService)
    {
        ArgumentNullException.ThrowIfNull(cronService);

        if (_config.Jobs.Count == 0)
        {
            _logger.LogInformation("No cron jobs configured under BotNexus:Cron:Jobs.");
            return;
        }

        foreach (var (jobKey, jobConfig) in _config.Jobs)
        {
            if (jobConfig is null)
            {
                _logger.LogWarning("Skipping cron job '{JobKey}' because configuration is null.", jobKey);
                continue;
            }

            try
            {
                var job = CreateJob(jobKey, jobConfig);
                if (job is null)
                    continue;

                cronService.Register(job);
                _logger.LogInformation(
                    "Registered cron job '{JobKey}' (name='{JobName}', type='{Type}', schedule='{Schedule}', enabled={Enabled})",
                    jobKey,
                    job.Name,
                    jobConfig.Type,
                    jobConfig.Schedule,
                    jobConfig.Enabled);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping invalid cron job '{JobKey}' (type='{Type}')", jobKey, jobConfig.Type);
            }
        }
    }

    private ICronJob? CreateJob(string jobKey, CronJobConfig jobConfig)
    {
        var type = jobConfig.Type?.Trim().ToLowerInvariant();
        return type switch
        {
            "agent" => CreateAgentJob(jobConfig),
            "system" => CreateSystemJob(jobConfig),
            "maintenance" => CreateMaintenanceJob(jobConfig),
            _ => HandleUnknownType(jobKey, jobConfig.Type)
        };
    }

    private ICronJob CreateAgentJob(CronJobConfig jobConfig)
    {
        var agentRunnerFactory = _serviceProvider.GetRequiredService<IAgentRunnerFactory>();
        var sessionManager = _serviceProvider.GetRequiredService<ISessionManager>();
        var jobLogger = _serviceProvider.GetService<ILogger<AgentCronJob>>();

        IChannel? ResolveChannel(string channelName)
            => _serviceProvider.GetServices<IChannel>()
                .FirstOrDefault(channel => string.Equals(channel.Name, channelName, StringComparison.OrdinalIgnoreCase));

        return new AgentCronJob(jobConfig, agentRunnerFactory, sessionManager, ResolveChannel, jobLogger);
    }

    private ICronJob CreateSystemJob(CronJobConfig jobConfig)
    {
        var actionRegistry = _serviceProvider.GetRequiredService<ISystemActionRegistry>();
        return new SystemCronJob(jobConfig, actionRegistry);
    }

    private ICronJob CreateMaintenanceJob(CronJobConfig jobConfig)
    {
        var memoryConsolidator = _serviceProvider.GetRequiredService<IMemoryConsolidator>();
        var sessionManager = _serviceProvider.GetRequiredService<ISessionManager>();
        return new MaintenanceCronJob(jobConfig, memoryConsolidator, sessionManager);
    }

    private ICronJob? HandleUnknownType(string jobKey, string? configuredType)
    {
        _logger.LogWarning(
            "Skipping cron job '{JobKey}' because type '{Type}' is not supported. Expected: agent, system, maintenance.",
            jobKey,
            configuredType ?? "<null>");
        return null;
    }
}

public sealed class CronJobRegistrationHostedService(
    CronJobFactory cronJobFactory,
    ICronService cronService,
    ILogger<CronJobRegistrationHostedService> logger) : IHostedService
{
    private readonly CronJobFactory _cronJobFactory = cronJobFactory ?? throw new ArgumentNullException(nameof(cronJobFactory));
    private readonly ICronService _cronService = cronService ?? throw new ArgumentNullException(nameof(cronService));
    private readonly ILogger<CronJobRegistrationHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _cronJobFactory.CreateAndRegisterAll(_cronService);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cron job registration failed during startup.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
