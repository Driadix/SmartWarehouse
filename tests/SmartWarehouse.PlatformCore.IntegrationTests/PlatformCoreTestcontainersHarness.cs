using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;
using Testcontainers.PostgreSql;

namespace SmartWarehouse.PlatformCore.IntegrationTests;

public sealed class PlatformCoreTestcontainersHarness : IAsyncLifetime
{
  private const string PostgreSqlImage = "postgres:18-alpine";
  private const string NatsImage = "nats:2.12-alpine";
  private const string PostgreSqlMaintenanceDatabase = "postgres";
  private const string PostgreSqlUsername = "smartwarehouse";
  private const string PostgreSqlPassword = "smartwarehouse";
  private const int PostgreSqlInternalPort = 5432;
  private const int NatsInternalPort = 4222;
  private const int NatsMonitoringInternalPort = 8222;
  private const int DependencyTimeoutSeconds = 3;
  private const int JetStreamStartupTimeoutSeconds = 20;
  private static readonly TimeSpan JetStreamPollingInterval = TimeSpan.FromMilliseconds(250);

  private readonly PostgreSqlContainer _postgreSqlContainer;
  private readonly IContainer _natsContainer;
  private bool _initialized;

  public PlatformCoreTestcontainersHarness()
  {
    var repositoryRoot = TestRepositoryRoot.Get();
    var natsConfigFile = new FileInfo(Path.Combine(repositoryRoot, "deploy", "local", "nats", "nats-server.conf"));

    if (!natsConfigFile.Exists)
    {
      throw new InvalidOperationException($"NATS config file was not found: '{natsConfigFile.FullName}'.");
    }

    _postgreSqlContainer = new PostgreSqlBuilder()
        .WithImage(PostgreSqlImage)
        .WithDatabase(PostgreSqlMaintenanceDatabase)
        .WithUsername(PostgreSqlUsername)
        .WithPassword(PostgreSqlPassword)
        .WithName($"smartwarehouse-tests-postgres-{Guid.NewGuid():N}")
        .WithCleanUp(true)
        .Build();

    _natsContainer = new ContainerBuilder()
        .WithImage(NatsImage)
        .WithName($"smartwarehouse-tests-nats-{Guid.NewGuid():N}")
        .WithCommand(["-js", "-c", "/etc/nats/nats-server.conf"])
        .WithResourceMapping(natsConfigFile, new FileInfo("/etc/nats/nats-server.conf"))
        .WithPortBinding(NatsInternalPort, true)
        .WithPortBinding(NatsMonitoringInternalPort, true)
        .WithWaitStrategy(
            Wait.ForUnixContainer()
                .UntilInternalTcpPortIsAvailable(NatsInternalPort)
                .UntilInternalTcpPortIsAvailable(NatsMonitoringInternalPort))
        .WithCleanUp(true)
        .Build();
  }

  public string PostgreSqlHost => _postgreSqlContainer.Hostname;

  public int PostgreSqlPort => _postgreSqlContainer.GetMappedPublicPort(PostgreSqlInternalPort);

  public string NatsHost => _natsContainer.Hostname;

  public int NatsPort => _natsContainer.GetMappedPublicPort(NatsInternalPort);

  public Uri NatsMonitoringBaseAddress => new($"http://{NatsHost}:{_natsContainer.GetMappedPublicPort(NatsMonitoringInternalPort)}/");

  public async Task InitializeAsync()
  {
    await Task.WhenAll(
        _postgreSqlContainer.StartAsync(),
        _natsContainer.StartAsync());

    await WaitForJetStreamAsync();
    _initialized = true;
  }

  public async Task DisposeAsync()
  {
    _initialized = false;

    await Task.WhenAll(
        _natsContainer.DisposeAsync().AsTask(),
        _postgreSqlContainer.DisposeAsync().AsTask());
  }

  public async Task<PlatformCoreIntegrationTestEnvironment> CreateEnvironmentAsync(CancellationToken cancellationToken = default)
  {
    EnsureInitialized();

    var scopeId = $"it_{Guid.NewGuid():N}";
    var databaseName = $"smartwarehouse_{scopeId}";

    await CreateDatabaseAsync(databaseName, cancellationToken);

    return new PlatformCoreIntegrationTestEnvironment(
        harness: this,
        scopeId: scopeId,
        databaseName: databaseName,
        platformCoreConnectionString: BuildConnectionString(databaseName),
        natsConnectionString: $"nats://{NatsHost}:{NatsPort}",
        natsMonitoringBaseAddress: NatsMonitoringBaseAddress,
        postgreSqlHost: PostgreSqlHost,
        postgreSqlPort: PostgreSqlPort,
        natsHost: NatsHost,
        natsPort: NatsPort);
  }

  internal async Task DropDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
  {
    EnsureInitialized();

    var escapedDatabaseName = EscapeIdentifier(databaseName);

    await using var connection = new NpgsqlConnection(BuildMaintenanceConnectionString());
    await connection.OpenAsync(cancellationToken);

    await using var command = new NpgsqlCommand(
        $"drop database if exists {escapedDatabaseName} with (force);",
        connection);

    await command.ExecuteNonQueryAsync(cancellationToken);
  }

  private async Task CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken)
  {
    var escapedDatabaseName = EscapeIdentifier(databaseName);

    await using var connection = new NpgsqlConnection(BuildMaintenanceConnectionString());
    await connection.OpenAsync(cancellationToken);

    await using var command = new NpgsqlCommand(
        $"create database {escapedDatabaseName};",
        connection);

    await command.ExecuteNonQueryAsync(cancellationToken);
  }

  private string BuildConnectionString(string databaseName)
  {
    var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_postgreSqlContainer.GetConnectionString())
    {
      Database = databaseName
    };

    return connectionStringBuilder.ConnectionString;
  }

  private string BuildMaintenanceConnectionString()
  {
    var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_postgreSqlContainer.GetConnectionString())
    {
      Database = PostgreSqlMaintenanceDatabase
    };

    return connectionStringBuilder.ConnectionString;
  }

  private async Task WaitForJetStreamAsync()
  {
    using var httpClient = new HttpClient
    {
      BaseAddress = NatsMonitoringBaseAddress,
      Timeout = TimeSpan.FromSeconds(DependencyTimeoutSeconds)
    };

    var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(JetStreamStartupTimeoutSeconds);

    while (DateTimeOffset.UtcNow < timeoutAt)
    {
      try
      {
        using var response = await httpClient.GetAsync("jsz?config=true");

        if (response.IsSuccessStatusCode)
        {
          return;
        }
      }
      catch (HttpRequestException)
      {
      }
      catch (TaskCanceledException)
      {
      }

      await Task.Delay(JetStreamPollingInterval);
    }

    throw new TimeoutException("NATS JetStream did not become ready within the expected time.");
  }

  private void EnsureInitialized()
  {
    if (!_initialized)
    {
      throw new InvalidOperationException("The shared integration test harness has not been initialized yet.");
    }
  }

  private static string EscapeIdentifier(string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
  }
}

public sealed class PlatformCoreIntegrationTestEnvironment : IAsyncDisposable
{
  private const int DependencyTimeoutSeconds = 3;
  private readonly PlatformCoreTestcontainersHarness _harness;

  internal PlatformCoreIntegrationTestEnvironment(
      PlatformCoreTestcontainersHarness harness,
      string scopeId,
      string databaseName,
      string platformCoreConnectionString,
      string natsConnectionString,
      Uri natsMonitoringBaseAddress,
      string postgreSqlHost,
      int postgreSqlPort,
      string natsHost,
      int natsPort)
  {
    _harness = harness;
    ScopeId = scopeId;
    DatabaseName = databaseName;
    PlatformCoreConnectionString = platformCoreConnectionString;
    NatsConnectionString = natsConnectionString;
    NatsMonitoringBaseAddress = natsMonitoringBaseAddress;
    PostgreSqlHost = postgreSqlHost;
    PostgreSqlPort = postgreSqlPort;
    NatsHost = natsHost;
    NatsPort = natsPort;
  }

  public string ScopeId { get; }

  public string PlatformCoreConnectionString { get; }

  public string NatsConnectionString { get; }

  public Uri NatsMonitoringBaseAddress { get; }

  public string PostgreSqlHost { get; }

  public int PostgreSqlPort { get; }

  public string NatsHost { get; }

  public int NatsPort { get; }

  public string NatsSubjectPrefix => $"tests.{ScopeId}";

  private string DatabaseName { get; }

  public IReadOnlyDictionary<string, string> CreateConfigurationOverrides()
  {
    var configuration = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      ["ConnectionStrings:PlatformCore"] = PlatformCoreConnectionString,
      ["HealthChecks:Dependencies:Postgres:Enabled"] = bool.TrueString,
      ["HealthChecks:Dependencies:Postgres:Host"] = PostgreSqlHost,
      ["HealthChecks:Dependencies:Postgres:Port"] = PostgreSqlPort.ToString(CultureInfo.InvariantCulture),
      ["HealthChecks:Dependencies:Postgres:TimeoutSeconds"] = DependencyTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
      ["HealthChecks:Dependencies:Nats:Enabled"] = bool.TrueString,
      ["HealthChecks:Dependencies:Nats:Host"] = NatsHost,
      ["HealthChecks:Dependencies:Nats:Port"] = NatsPort.ToString(CultureInfo.InvariantCulture),
      ["HealthChecks:Dependencies:Nats:TimeoutSeconds"] = DependencyTimeoutSeconds.ToString(CultureInfo.InvariantCulture)
    };

    return new ReadOnlyDictionary<string, string>(configuration);
  }

  public IReadOnlyDictionary<string, string> CreateProcessEnvironmentVariables()
  {
    var environmentVariables = CreateConfigurationOverrides()
        .ToDictionary(
            pair => pair.Key.Replace(":", "__", StringComparison.Ordinal),
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

    return new ReadOnlyDictionary<string, string>(environmentVariables);
  }

  public void ApplyProcessEnvironment(ProcessStartInfo startInfo)
  {
    ArgumentNullException.ThrowIfNull(startInfo);

    foreach (var (key, value) in CreateProcessEnvironmentVariables())
    {
      startInfo.Environment[key] = value;
    }
  }

  public async ValueTask DisposeAsync()
  {
    await _harness.DropDatabaseAsync(DatabaseName);
  }
}
