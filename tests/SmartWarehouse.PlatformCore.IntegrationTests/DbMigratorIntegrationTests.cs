using System.Diagnostics;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartWarehouse.PlatformCore.DbMigrator;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;

namespace SmartWarehouse.PlatformCore.IntegrationTests;

[Collection(PlatformCoreIntegrationFixtureDefinition.Name)]
public sealed class DbMigratorIntegrationTests
{
  private readonly PlatformCoreTestcontainersHarness _harness;

  public DbMigratorIntegrationTests(PlatformCoreTestcontainersHarness harness)
  {
    _harness = harness;
  }

  [Fact]
  public async Task SharedHarnessStartsPostgreSqlAndNatsJetStream()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();

    await using var connection = new NpgsqlConnection(environment.PlatformCoreConnectionString);
    await connection.OpenAsync();

    await using var command = new NpgsqlCommand("select 1;", connection);
    var scalar = (int)(await command.ExecuteScalarAsync())!;

    Assert.Equal(1, scalar);

    using var httpClient = new HttpClient
    {
      BaseAddress = environment.NatsMonitoringBaseAddress,
      Timeout = TimeSpan.FromSeconds(3)
    };

    using var response = await httpClient.GetAsync("jsz?config=true");
    response.EnsureSuccessStatusCode();
  }

  [Fact]
  public async Task DbMigratorAppliesPlatformCoreSchemaToFreshDatabase()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();

    var result = await RunDbMigratorAsync(environment.CreateProcessEnvironmentVariables());
    var logOutput = result.StandardOutput + Environment.NewLine + result.StandardError;

    Assert.Equal((int)DbMigratorExitCode.Success, result.ExitCode);
    Assert.Contains("Database schema is up to date.", logOutput, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("Failed executing DbCommand", logOutput, StringComparison.OrdinalIgnoreCase);

    await using var context = CreateContext(environment.PlatformCoreConnectionString);
    var availableMigrationIds = context.Database.GetMigrations().ToArray();
    var appliedMigrationIds = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
    var pendingMigrationIds = (await context.Database.GetPendingMigrationsAsync()).ToArray();

    Assert.Equal(availableMigrationIds, appliedMigrationIds);
    Assert.Empty(pendingMigrationIds);

    await using var connection = new NpgsqlConnection(environment.PlatformCoreConnectionString);
    await connection.OpenAsync();

    foreach (var schema in PersistenceSchemas.All)
    {
      Assert.True(await SchemaExistsAsync(connection, schema), $"Schema '{schema}' was not created.");
    }

    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Config, "topology_versions"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Config, "topology_levels"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Config, "topology_nodes"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Config, "topology_edges"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Config, "topology_shafts"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Config, "topology_shaft_stops"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Config, "topology_stations"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Config, "topology_service_points"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Config, "device_bindings"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Config, "endpoint_mappings"));

    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_versions", "TopologyId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_versions", "Version"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_levels", "LevelId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_levels", "Ordinal"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_nodes", "LevelId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_nodes", "Tags"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_nodes", "StationId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_nodes", "ShaftId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_nodes", "ServicePointId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_shafts", "CarrierDeviceId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_shaft_stops", "LevelId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_shaft_stops", "CarrierNodeId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_shaft_stops", "TransferPointId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "topology_service_points", "PassiveSemantics"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "device_bindings", "InitialNodeId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "device_bindings", "HomeNodeId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "device_bindings", "ShaftId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "endpoint_mappings", "EndpointKind"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "endpoint_mappings", "StationId"));
    Assert.True(await ColumnExistsAsync(connection, PersistenceSchemas.Config, "endpoint_mappings", "ServicePointId"));

    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Integration, "__ef_migrations_history"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Integration, "outbox_messages"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Integration, "inbox_messages"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Integration, "northbound_idempotency"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Integration, "webhook_deliveries"));
    Assert.True(await TableExistsAsync(connection, PersistenceSchemas.Audit, "platform_event_journal"));
  }

  [Fact]
  public async Task DbMigratorIsIdempotentWhenDatabaseAlreadyUpToDate()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();

    var firstRun = await RunDbMigratorAsync(environment.CreateProcessEnvironmentVariables());
    var secondRun = await RunDbMigratorAsync(environment.CreateProcessEnvironmentVariables());

    Assert.Equal((int)DbMigratorExitCode.Success, firstRun.ExitCode);
    Assert.Equal((int)DbMigratorExitCode.Success, secondRun.ExitCode);

    await using var context = CreateContext(environment.PlatformCoreConnectionString);
    var availableMigrationIds = context.Database.GetMigrations().ToArray();
    var appliedMigrationIds = (await context.Database.GetAppliedMigrationsAsync()).ToArray();

    Assert.Equal(availableMigrationIds, appliedMigrationIds);
  }

  [Fact]
  public async Task DbMigratorReturnsMissingConnectionStringExitCode()
  {
    var result = await RunDbMigratorAsync();

    Assert.Equal((int)DbMigratorExitCode.MissingConnectionString, result.ExitCode);
    Assert.Contains("Connection string 'PlatformCore' is required.", result.StandardError, StringComparison.Ordinal);
  }

  [Fact]
  public async Task DbMigratorReturnsFailureExitCodeWhenDatabaseIsUnavailable()
  {
    const string unreachableConnectionString =
        "Host=127.0.0.1;Port=1;Database=smartwarehouse;Username=smartwarehouse;Password=smartwarehouse;Timeout=2;Command Timeout=2";

    var result = await RunDbMigratorAsync(
        new Dictionary<string, string>
        {
          ["ConnectionStrings__PlatformCore"] = unreachableConnectionString
        });

    Assert.Equal((int)DbMigratorExitCode.MigrationFailed, result.ExitCode);
    Assert.Contains("Database migration failed.", result.StandardError + result.StandardOutput, StringComparison.OrdinalIgnoreCase);
  }

  private static PlatformCoreDbContext CreateContext(string connectionString)
  {
    var options = new DbContextOptionsBuilder<PlatformCoreDbContext>()
        .UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", PersistenceSchemas.Integration))
        .Options;

    return new PlatformCoreDbContext(options);
  }

  private static async Task<bool> SchemaExistsAsync(NpgsqlConnection connection, string schemaName)
  {
    await using var command = new NpgsqlCommand(
        """
        select exists (
          select 1
          from information_schema.schemata
          where schema_name = @schemaName
        );
        """,
        connection);

    command.Parameters.AddWithValue("schemaName", schemaName);

    return (bool)(await command.ExecuteScalarAsync())!;
  }

  private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string schemaName, string tableName)
  {
    await using var command = new NpgsqlCommand(
        """
        select exists (
          select 1
          from information_schema.tables
          where table_schema = @schemaName
            and table_name = @tableName
        );
        """,
        connection);

    command.Parameters.AddWithValue("schemaName", schemaName);
    command.Parameters.AddWithValue("tableName", tableName);

    return (bool)(await command.ExecuteScalarAsync())!;
  }

  private static async Task<bool> ColumnExistsAsync(
      NpgsqlConnection connection,
      string schemaName,
      string tableName,
      string columnName)
  {
    await using var command = new NpgsqlCommand(
        """
        select exists (
          select 1
          from information_schema.columns
          where table_schema = @schemaName
            and table_name = @tableName
            and column_name = @columnName
        );
        """,
        connection);

    command.Parameters.AddWithValue("schemaName", schemaName);
    command.Parameters.AddWithValue("tableName", tableName);
    command.Parameters.AddWithValue("columnName", columnName);

    return (bool)(await command.ExecuteScalarAsync())!;
  }

  private static async Task<DbMigratorProcessResult> RunDbMigratorAsync(
      IReadOnlyDictionary<string, string>? environmentVariables = null)
  {
    var migratorAssemblyPath = GetDbMigratorAssemblyPath();
    var startInfo = new ProcessStartInfo("dotnet")
    {
      Arguments = $"\"{migratorAssemblyPath}\"",
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      WorkingDirectory = TestRepositoryRoot.Get()
    };

    startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
    startInfo.Environment.Remove("ConnectionStrings__PlatformCore");
    startInfo.Environment.Remove("PlatformCore__ConnectionString");

    if (environmentVariables is not null)
    {
      foreach (var (key, value) in environmentVariables)
      {
        startInfo.Environment[key] = value;
      }
    }

    using var process = new Process { StartInfo = startInfo };
    process.Start();

    var standardOutputTask = process.StandardOutput.ReadToEndAsync();
    var standardErrorTask = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));

    try
    {
      await process.WaitForExitAsync(timeout.Token);
    }
    catch (OperationCanceledException)
    {
      if (!process.HasExited)
      {
        process.Kill(entireProcessTree: true);
      }

      throw;
    }

    return new DbMigratorProcessResult(
        process.ExitCode,
        await standardOutputTask,
        await standardErrorTask);
  }

  private static string GetDbMigratorAssemblyPath()
  {
    var (configuration, targetFramework) = GetCurrentBuildCoordinates();
    var assemblyPath = Path.Combine(
        TestRepositoryRoot.Get(),
        "src",
        "platform-core",
        "SmartWarehouse.PlatformCore.DbMigrator",
        "bin",
        configuration,
        targetFramework,
        "SmartWarehouse.PlatformCore.DbMigrator.dll");

    if (!File.Exists(assemblyPath))
    {
      throw new FileNotFoundException($"DbMigrator assembly was not found at '{assemblyPath}'.", assemblyPath);
    }

    return assemblyPath;
  }

  private static (string Configuration, string TargetFramework) GetCurrentBuildCoordinates()
  {
    var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
    var targetFramework = baseDirectory.Name;
    var configuration = baseDirectory.Parent?.Name;

    if (string.IsNullOrWhiteSpace(configuration) || string.IsNullOrWhiteSpace(targetFramework))
    {
      throw new InvalidOperationException($"Unable to infer build coordinates from '{AppContext.BaseDirectory}'.");
    }

    return (configuration, targetFramework);
  }

  private sealed record DbMigratorProcessResult(
      int ExitCode,
      string StandardOutput,
      string StandardError);
}
