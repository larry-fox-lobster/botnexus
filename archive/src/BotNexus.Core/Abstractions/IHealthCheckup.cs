namespace BotNexus.Core.Abstractions;

public interface IHealthCheckup
{
    string Name { get; }
    string Category { get; }
    string Description { get; }
    bool CanAutoFix => false;
    Task<CheckupResult> RunAsync(CancellationToken ct = default);
    Task<CheckupResult> FixAsync(CancellationToken ct = default) => RunAsync(ct);
}

public record CheckupResult(CheckupStatus Status, string Message, string? Advice = null);

public enum CheckupStatus
{
    Pass,
    Warn,
    Fail
}
