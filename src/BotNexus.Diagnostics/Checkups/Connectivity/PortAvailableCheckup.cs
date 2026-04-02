using System.Net;
using System.Net.Sockets;
using BotNexus.Core.Abstractions;
using BotNexus.Core.Configuration;
using Microsoft.Extensions.Options;

namespace BotNexus.Diagnostics.Checkups.Connectivity;

public sealed class PortAvailableCheckup(IOptions<BotNexusConfig> options) : IHealthCheckup
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(1);
    private readonly BotNexusConfig _config = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public string Name => "PortAvailable";
    public string Category => "Connectivity";
    public string Description => "Checks whether the configured gateway port is available when gateway is not running.";

    public async Task<CheckupResult> RunAsync(CancellationToken ct = default)
    {
        try
        {
            var port = _config.Gateway.Port;
            if (port is <= 0 or > 65535)
            {
                return new CheckupResult(
                    CheckupStatus.Fail,
                    $"Gateway port '{port}' is invalid.",
                    "Set BotNexus:Gateway:Port to a value between 1 and 65535.");
            }

            if (await IsGatewayReachableAsync(port, ct).ConfigureAwait(false))
            {
                return new CheckupResult(
                    CheckupStatus.Pass,
                    $"Gateway appears to be running on port {port}; availability check skipped.");
            }

            using var listener = new TcpListener(IPAddress.Any, port);
            try
            {
                listener.Start();
                return new CheckupResult(CheckupStatus.Pass, $"Gateway port {port} is available.");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                return new CheckupResult(
                    CheckupStatus.Fail,
                    $"Gateway port {port} is already in use.",
                    "Stop the process using this port or change BotNexus:Gateway:Port.");
            }
            finally
            {
                listener.Stop();
            }
        }
        catch (Exception ex)
        {
            return new CheckupResult(
                CheckupStatus.Fail,
                $"Failed to check gateway port availability: {ex.Message}",
                "Verify gateway host/port configuration and local networking permissions.");
        }
    }

    private async Task<bool> IsGatewayReachableAsync(int port, CancellationToken ct)
    {
        using var tcpClient = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(ProbeTimeout);

        try
        {
            await tcpClient.ConnectAsync(IPAddress.Loopback, port, timeoutCts.Token).ConfigureAwait(false);
            return tcpClient.Connected;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
