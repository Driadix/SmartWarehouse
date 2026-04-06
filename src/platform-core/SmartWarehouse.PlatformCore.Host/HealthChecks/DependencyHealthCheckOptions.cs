namespace SmartWarehouse.PlatformCore.Host.HealthChecks;

public sealed class DependencyHealthCheckOptions
{
  public bool Enabled { get; init; }

  public string? Host { get; init; }

  public int Port { get; init; }

  public int TimeoutSeconds { get; init; } = 3;
}
