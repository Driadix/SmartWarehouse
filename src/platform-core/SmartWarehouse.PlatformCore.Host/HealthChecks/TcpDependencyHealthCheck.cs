using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SmartWarehouse.PlatformCore.Host.HealthChecks;

public sealed class TcpDependencyHealthCheck(
    string dependencyName,
    string host,
    int port,
    TimeSpan timeout) : IHealthCheck
{
  public async Task<HealthCheckResult> CheckHealthAsync(
      HealthCheckContext context,
      CancellationToken cancellationToken = default)
  {
    IReadOnlyDictionary<string, object> data = new Dictionary<string, object>
    {
      ["dependency"] = dependencyName,
      ["host"] = host,
      ["port"] = port
    };

    using var tcpClient = new TcpClient();
    using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeoutCancellation.CancelAfter(timeout);

    try
    {
      await tcpClient.ConnectAsync(host, port, timeoutCancellation.Token);
      return HealthCheckResult.Healthy("TCP endpoint is reachable.", data);
    }
    catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
    {
      return HealthCheckResult.Unhealthy("TCP endpoint health check timed out.", exception, data);
    }
    catch (Exception exception)
    {
      return HealthCheckResult.Unhealthy("TCP endpoint is unreachable.", exception, data);
    }
  }
}
