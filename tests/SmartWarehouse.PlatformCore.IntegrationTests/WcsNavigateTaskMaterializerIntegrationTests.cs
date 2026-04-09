using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Contracts;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Application.Wcs;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Operations;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;
using SmartWarehouse.PlatformCore.Infrastructure.Wcs;

namespace SmartWarehouse.PlatformCore.IntegrationTests;

[Collection(PlatformCoreIntegrationFixtureDefinition.Name)]
public sealed class WcsNavigateTaskMaterializerIntegrationTests
{
  private readonly PlatformCoreTestcontainersHarness _harness;

  public WcsNavigateTaskMaterializerIntegrationTests(PlatformCoreTestcontainersHarness harness)
  {
    _harness = harness;
  }

  [Fact]
  public async Task MaterializeAuthorizesShortMotionWindowAndPersistsInternalCommandIdempotently()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();
    var processor = scope.ServiceProvider.GetRequiredService<IWcsExecutionTaskCommandProcessor>();
    var materializer = scope.ServiceProvider.GetRequiredService<IWcsNavigateTaskMaterializer>();

    await store.EnsureInitializedAsync();
    await store.UpsertDeviceSessionAsync(
        new DeviceSession(
            new DeviceSessionId("session-nav-01"),
            new DeviceId("SHUTTLE_01"),
            new DeviceSessionState("LEASED"),
            leaseUntil: DateTimeOffset.UtcNow.AddMinutes(5),
            lastHeartbeatAt: DateTimeOffset.UtcNow));
    await processor.SubmitAsync(CreateSubmitCommand(targetNodeId: "L1_TP_LIFT_A"));

    var firstResult = await materializer.MaterializeAsync(new ExecutionTaskId("task-nav-01"));
    var secondResult = await materializer.MaterializeAsync(new ExecutionTaskId("task-nav-01"));

    Assert.Equal(NavigateMaterializationStatus.MotionAuthorized, firstResult.Status);
    Assert.Equal(["L1_SWITCH_A"], firstResult.AuthorizedNodePath.Select(static nodeId => nodeId.Value).ToArray());
    Assert.Equal(NavigateMaterializationStatus.AlreadyAuthorized, secondResult.Status);
    Assert.Equal(["L1_SWITCH_A"], secondResult.AuthorizedNodePath.Select(static nodeId => nodeId.Value).ToArray());
    Assert.Equal(firstResult.OutboxId, secondResult.OutboxId);

    var runtime = await LoadRuntimeAsync(environment.PlatformCoreConnectionString, "task-nav-01");
    var reservation = await LoadReservationAsync(environment.PlatformCoreConnectionString, "task-nav-01");
    var shuttleShadow = await LoadDeviceShadowAsync(environment.PlatformCoreConnectionString, "SHUTTLE_01");
    var outboxMessages = await LoadTaskOutboxMessagesAsync(environment.PlatformCoreConnectionString, "task-nav-01");

    Assert.Equal(ExecutionTaskState.InProgress, runtime.State);
    Assert.Equal("MotionAuthorized", runtime.ActiveRuntimePhase);
    Assert.Null(runtime.ReasonCode);
    Assert.Null(runtime.ResolutionHint);
    Assert.Null(runtime.ReplanRequired);

    Assert.Equal(ReservationHorizon.Execution, reservation.Horizon);
    Assert.Equal("ACTIVE", reservation.State);
    Assert.Equal("ExecutionTask", reservation.OwnerType);
    Assert.Equal("task-nav-01", reservation.OwnerId);
    Assert.Equal(["L1_SWITCH_A"], reservation.ReservedNodeIds);

    Assert.Equal(DeviceExecutionState.Executing, shuttleShadow.ExecutionState);
    Assert.Equal(DispatchStatus.Occupied, shuttleShadow.DispatchStatus);

    var outboxMessage = Assert.Single(outboxMessages);
    Assert.Equal("WCS", outboxMessage.Producer);
    Assert.Equal("INTERNAL_COMMAND", outboxMessage.MessageKind);
    Assert.Equal("ExecutionTask", outboxMessage.AggregateType);
    Assert.Equal("task-nav-01", outboxMessage.AggregateId);
    Assert.Equal("corr-nav-01", outboxMessage.CorrelationId);

    using var document = JsonDocument.Parse(outboxMessage.Payload);
    var root = document.RootElement;
    Assert.Equal("GrantMotionWindow", root.GetProperty("messageType").GetString());
    Assert.Equal("v0", root.GetProperty("schemaVersion").GetString());
    Assert.Equal("corr-nav-01", root.GetProperty("correlationId").GetString());
    Assert.Equal("task-nav-01", root.GetProperty("causationId").GetString());
    Assert.Equal("SHUTTLE_01", root.GetProperty("deviceId").GetString());
    Assert.Equal("Shuttle3D", root.GetProperty("family").GetString());
    Assert.Equal("session-nav-01", root.GetProperty("sessionId").GetString());
    Assert.Equal(["L1_SWITCH_A"], root.GetProperty("payload").GetProperty("nodePath").EnumerateArray().Select(static value => value.GetString()!).ToArray());
  }

  [Fact]
  public async Task MaterializeSuspendsWhenImmediateConflictNodeIsReserved()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();
    var processor = scope.ServiceProvider.GetRequiredService<IWcsExecutionTaskCommandProcessor>();
    var materializer = scope.ServiceProvider.GetRequiredService<IWcsNavigateTaskMaterializer>();

    await store.EnsureInitializedAsync();
    await store.UpsertDeviceSessionAsync(
        new DeviceSession(
            new DeviceSessionId("session-nav-02"),
            new DeviceId("SHUTTLE_01"),
            new DeviceSessionState("LEASED"),
            leaseUntil: DateTimeOffset.UtcNow.AddMinutes(5),
            lastHeartbeatAt: DateTimeOffset.UtcNow));
    await store.UpsertReservationAsync(
        new Reservation(
            new ReservationId("reservation-blocking"),
            ReservationOwnerRef.ForExecutionTask(new ExecutionTaskId("other-task")),
            [new NodeId("L1_SWITCH_A")],
            ReservationHorizon.Execution,
            new ReservationState("ACTIVE")));
    await processor.SubmitAsync(CreateSubmitCommand(targetNodeId: "L1_TP_LIFT_A"));

    var result = await materializer.MaterializeAsync(new ExecutionTaskId("task-nav-01"));

    Assert.Equal(NavigateMaterializationStatus.Suspended, result.Status);
    Assert.Empty(result.AuthorizedNodePath);
    Assert.Null(result.OutboxId);

    var runtime = await LoadRuntimeAsync(environment.PlatformCoreConnectionString, "task-nav-01");
    var shuttleShadow = await LoadDeviceShadowAsync(environment.PlatformCoreConnectionString, "SHUTTLE_01");
    var taskOutboxMessages = await LoadTaskOutboxMessagesAsync(environment.PlatformCoreConnectionString, "task-nav-01");

    Assert.Equal(ExecutionTaskState.Suspended, runtime.State);
    Assert.Equal("AwaitingMotionWindow", runtime.ActiveRuntimePhase);
    Assert.Equal("MOTION_WINDOW_BLOCKED", runtime.ReasonCode);
    Assert.Equal(ExecutionResolutionHint.WaitAndRetry, runtime.ResolutionHint);
    Assert.False(runtime.ReplanRequired);
    Assert.Equal(DeviceExecutionState.Idle, shuttleShadow.ExecutionState);
    Assert.Equal(DispatchStatus.Available, shuttleShadow.DispatchStatus);
    Assert.Empty(taskOutboxMessages);
  }

  private static SubmitExecutionTask CreateSubmitCommand(string targetNodeId) =>
      new(
          new ContractEnvelope(
              new EnvelopeId("msg-nav-01"),
              new CorrelationId("corr-nav-01")),
          new ExecutionTaskId("task-nav-01"),
          new TaskRevision(1),
          new JobId("job-nav-01"),
          ExecutionTaskType.Navigate,
          ExecutionResourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
          [],
          targetNode: new NodeId(targetNodeId));

  private static async Task ApplyMigrationsAsync(string connectionString)
  {
    await using var context = CreateContext(connectionString);
    await context.Database.MigrateAsync();
  }

  private static async Task<ExecutionTaskRuntimeRecord> LoadRuntimeAsync(string connectionString, string executionTaskId)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<ExecutionTaskRuntimeRecord>()
        .AsNoTracking()
        .SingleAsync(record => record.ExecutionTaskId == executionTaskId);
  }

  private static async Task<ReservationRecord> LoadReservationAsync(string connectionString, string executionTaskId)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<ReservationRecord>()
        .AsNoTracking()
        .SingleAsync(record => record.OwnerType == "ExecutionTask" && record.OwnerId == executionTaskId && record.State == "ACTIVE");
  }

  private static async Task<DeviceShadowRecord> LoadDeviceShadowAsync(string connectionString, string deviceId)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<DeviceShadowRecord>()
        .AsNoTracking()
        .SingleAsync(record => record.DeviceId == deviceId);
  }

  private static async Task<IReadOnlyList<OutboxMessageRecord>> LoadTaskOutboxMessagesAsync(string connectionString, string executionTaskId)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<OutboxMessageRecord>()
        .AsNoTracking()
        .Where(record => record.AggregateType == "ExecutionTask" && record.AggregateId == executionTaskId)
        .OrderBy(record => record.CreatedAt)
        .ThenBy(record => record.OutboxId)
        .ToListAsync();
  }

  private static ServiceProvider CreateServiceProvider(string connectionString)
  {
    var services = new ServiceCollection();
    services.AddPlatformCorePersistence(connectionString);
    services.AddWarehouseTopologyServices();
    services.AddSingleton(CreateCompiledTopology());
    services.AddPersistenceWcsOperationalStateStore();
    services.AddPersistenceWcsNavigateTaskMaterialization();

    return services.BuildServiceProvider(new ServiceProviderOptions
    {
      ValidateOnBuild = true,
      ValidateScopes = true
    });
  }

  private static CompiledWarehouseTopology CreateCompiledTopology()
  {
    var loader = new YamlWarehouseTopologyConfigLoader();
    var compiler = new WarehouseTopologyCompiler(new WarehouseTopologyConfigValidator());
    var topologyPath = Path.Combine(TestRepositoryRoot.Get(), "topologies", "phase1", "warehouse-a.nominal.yaml");

    return compiler.Compile(loader.LoadFromFile(topologyPath));
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
}
