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
public sealed class WcsStationTransferTaskMaterializerIntegrationTests
{
  private readonly PlatformCoreTestcontainersHarness _harness;

  public WcsStationTransferTaskMaterializerIntegrationTests(PlatformCoreTestcontainersHarness harness)
  {
    _harness = harness;
  }

  [Fact]
  public async Task MaterializeConfirmsBoundaryPositionButDoesNotCompleteWithoutConfirmedTransferFact()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();
    var processor = scope.ServiceProvider.GetRequiredService<IWcsExecutionTaskCommandProcessor>();
    var materializer = scope.ServiceProvider.GetRequiredService<IWcsStationTransferTaskMaterializer>();

    await store.EnsureInitializedAsync();
    await store.UpsertDeviceSessionAsync(
        new DeviceSession(
            new DeviceSessionId("session-station-01"),
            new DeviceId("SHUTTLE_01"),
            new DeviceSessionState("LEASED"),
            leaseUntil: DateTimeOffset.UtcNow.AddMinutes(5),
            lastHeartbeatAt: DateTimeOffset.UtcNow));
    await SetStationStateAsync(
        environment.PlatformCoreConnectionString,
        stationId: "LOAD_01",
        readiness: StationReadiness.Ready,
        currentPayloadId: "payload-load-01");
    await processor.SubmitAsync(CreateLoadSubmitCommand());

    var firstResult = await materializer.MaterializeAsync(new ExecutionTaskId("task-station-load-01"));
    await SetShuttleStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L1_LOAD_01",
        carriedPayloadId: null,
        dispatchStatus: DispatchStatus.Occupied,
        executionState: DeviceExecutionState.Executing);
    var secondResult = await materializer.MaterializeAsync(new ExecutionTaskId("task-station-load-01"));

    Assert.Equal(StationTransferMaterializationStatus.BoundaryMotionAuthorized, firstResult.Status);
    Assert.Equal(["L1_LOAD_01"], firstResult.AuthorizedNodePath.Select(static nodeId => nodeId.Value).ToArray());
    Assert.Equal(StationTransferMaterializationStatus.BoundaryPositionConfirmed, secondResult.Status);

    var runtime = await LoadRuntimeAsync(environment.PlatformCoreConnectionString, "task-station-load-01");
    var station = await LoadStationAsync(environment.PlatformCoreConnectionString, "LOAD_01");
    var shuttle = await LoadDeviceShadowAsync(environment.PlatformCoreConnectionString, "SHUTTLE_01");
    var outboxMessages = await LoadTaskOutboxMessagesAsync(environment.PlatformCoreConnectionString, "task-station-load-01");
    var journal = await LoadJournalAsync(environment.PlatformCoreConnectionString, "corr-station-load-01");

    Assert.Equal(ExecutionTaskState.InProgress, runtime.State);
    Assert.Equal("BoundaryPositionConfirmed", runtime.ActiveRuntimePhase);
    Assert.Null(runtime.ReasonCode);
    Assert.Null(runtime.ResolutionHint);
    Assert.Null(runtime.ReplanRequired);

    Assert.Equal("payload-load-01", station.CurrentPayloadId);
    Assert.Null(shuttle.CarriedPayloadId);
    Assert.Equal(DeviceExecutionState.Executing, shuttle.ExecutionState);
    Assert.Equal(DispatchStatus.Occupied, shuttle.DispatchStatus);

    var command = Assert.Single(outboxMessages);
    Assert.Equal("INTERNAL_COMMAND", command.MessageKind);
    Assert.Equal("task-station-load-01", command.AggregateId);
    Assert.Empty(journal);
  }

  [Fact]
  public async Task MaterializeSuspendsWhenStationBoundaryIsReachedButReadinessIsBlocked()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();
    var processor = scope.ServiceProvider.GetRequiredService<IWcsExecutionTaskCommandProcessor>();
    var materializer = scope.ServiceProvider.GetRequiredService<IWcsStationTransferTaskMaterializer>();

    await store.EnsureInitializedAsync();
    await SetStationStateAsync(
        environment.PlatformCoreConnectionString,
        stationId: "LOAD_01",
        readiness: StationReadiness.Blocked,
        currentPayloadId: "payload-load-02");
    await SetShuttleStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L1_LOAD_01",
        carriedPayloadId: null,
        dispatchStatus: DispatchStatus.Occupied,
        executionState: DeviceExecutionState.Executing);
    await processor.SubmitAsync(CreateLoadSubmitCommand());

    var result = await materializer.MaterializeAsync(new ExecutionTaskId("task-station-load-01"));

    Assert.Equal(StationTransferMaterializationStatus.Suspended, result.Status);

    var runtime = await LoadRuntimeAsync(environment.PlatformCoreConnectionString, "task-station-load-01");
    var outboxMessages = await LoadTaskOutboxMessagesAsync(environment.PlatformCoreConnectionString, "task-station-load-01");

    Assert.Equal(ExecutionTaskState.Suspended, runtime.State);
    Assert.Equal("AwaitingStationReadiness", runtime.ActiveRuntimePhase);
    Assert.Equal("STATION_NOT_READY", runtime.ReasonCode);
    Assert.Equal(ExecutionResolutionHint.WaitAndRetry, runtime.ResolutionHint);
    Assert.False(runtime.ReplanRequired);
    Assert.Empty(outboxMessages);
  }

  [Fact]
  public async Task ConfirmTransferCompletesLoadStepAndPublishesPayloadCustodyChanged()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();
    var processor = scope.ServiceProvider.GetRequiredService<IWcsExecutionTaskCommandProcessor>();
    var materializer = scope.ServiceProvider.GetRequiredService<IWcsStationTransferTaskMaterializer>();

    await store.EnsureInitializedAsync();
    await store.UpsertDeviceSessionAsync(
        new DeviceSession(
            new DeviceSessionId("session-station-02"),
            new DeviceId("SHUTTLE_01"),
            new DeviceSessionState("LEASED"),
            leaseUntil: DateTimeOffset.UtcNow.AddMinutes(5),
            lastHeartbeatAt: DateTimeOffset.UtcNow));
    await SetStationStateAsync(
        environment.PlatformCoreConnectionString,
        stationId: "LOAD_01",
        readiness: StationReadiness.Ready,
        currentPayloadId: "payload-load-03");
    await processor.SubmitAsync(CreateLoadSubmitCommand());

    var firstResult = await materializer.MaterializeAsync(new ExecutionTaskId("task-station-load-01"));
    await SetShuttleStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L1_LOAD_01",
        carriedPayloadId: null,
        dispatchStatus: DispatchStatus.Occupied,
        executionState: DeviceExecutionState.Executing);
    var secondResult = await materializer.MaterializeAsync(new ExecutionTaskId("task-station-load-01"));
    var completed = await materializer.ConfirmTransferAsync(
        new ExecutionTaskId("task-station-load-01"),
        new PayloadId("payload-load-03"));

    Assert.Equal(StationTransferMaterializationStatus.BoundaryMotionAuthorized, firstResult.Status);
    Assert.Equal(StationTransferMaterializationStatus.BoundaryPositionConfirmed, secondResult.Status);
    Assert.Equal(StationTransferMaterializationStatus.Completed, completed.Status);

    var runtime = await LoadRuntimeAsync(environment.PlatformCoreConnectionString, "task-station-load-01");
    var reservation = await LoadReservationAsync(environment.PlatformCoreConnectionString, "task-station-load-01");
    var station = await LoadStationAsync(environment.PlatformCoreConnectionString, "LOAD_01");
    var shuttle = await LoadDeviceShadowAsync(environment.PlatformCoreConnectionString, "SHUTTLE_01");
    var outboxMessages = await LoadTaskOutboxMessagesAsync(environment.PlatformCoreConnectionString, "task-station-load-01");
    var platformEvent = Assert.Single(outboxMessages, static message => message.MessageKind == "PLATFORM_EVENT");
    var journal = await LoadJournalAsync(environment.PlatformCoreConnectionString, "corr-station-load-01");
    var journalEvent = Assert.Single(journal);

    Assert.Equal(ExecutionTaskState.Completed, runtime.State);
    Assert.Equal("Completed", runtime.ActiveRuntimePhase);
    Assert.Null(runtime.ReasonCode);
    Assert.Equal("RELEASED", reservation.State);

    Assert.Null(station.CurrentPayloadId);
    Assert.Equal("payload-load-03", shuttle.CarriedPayloadId);
    Assert.Equal(DeviceExecutionState.Idle, shuttle.ExecutionState);
    Assert.Equal(DispatchStatus.Occupied, shuttle.DispatchStatus);

    Assert.Equal("PayloadCustodyChanged", journalEvent.EventName);
    Assert.Equal(PlatformEventVisibility.Operations, journalEvent.Visibility);

    using var outboxPayload = JsonDocument.Parse(platformEvent.Payload);
    var root = outboxPayload.RootElement;
    Assert.Equal("PayloadCustodyChanged", root.GetProperty("eventName").GetString());
    Assert.Equal("corr-station-load-01", root.GetProperty("correlationId").GetString());
    Assert.Equal("task-station-load-01", root.GetProperty("causationId").GetString());
    var payload = root.GetProperty("payload");
    Assert.Equal("payload-load-03", payload.GetProperty("payloadId").GetString());
    Assert.Equal("StationBoundary", payload.GetProperty("previousHolderType").GetString());
    Assert.Equal("LOAD_01", payload.GetProperty("previousHolderId").GetString());
    Assert.Equal("Device", payload.GetProperty("newHolderType").GetString());
    Assert.Equal("SHUTTLE_01", payload.GetProperty("newHolderId").GetString());
  }

  [Fact]
  public async Task ConfirmTransferCompletesUnloadStepAndMovesCustodyToStation()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();
    var processor = scope.ServiceProvider.GetRequiredService<IWcsExecutionTaskCommandProcessor>();
    var materializer = scope.ServiceProvider.GetRequiredService<IWcsStationTransferTaskMaterializer>();

    await store.EnsureInitializedAsync();
    await SetStationStateAsync(
        environment.PlatformCoreConnectionString,
        stationId: "UNLOAD_01",
        readiness: StationReadiness.Ready,
        currentPayloadId: null);
    await SetShuttleStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L2_UNLOAD_01",
        carriedPayloadId: "payload-unload-01",
        dispatchStatus: DispatchStatus.Occupied,
        executionState: DeviceExecutionState.Executing);
    await processor.SubmitAsync(CreateUnloadSubmitCommand());

    var positioned = await materializer.MaterializeAsync(new ExecutionTaskId("task-station-unload-01"));
    var completed = await materializer.ConfirmTransferAsync(
        new ExecutionTaskId("task-station-unload-01"),
        new PayloadId("payload-unload-01"));

    Assert.Equal(StationTransferMaterializationStatus.BoundaryPositionConfirmed, positioned.Status);
    Assert.Equal(StationTransferMaterializationStatus.Completed, completed.Status);

    var runtime = await LoadRuntimeAsync(environment.PlatformCoreConnectionString, "task-station-unload-01");
    var station = await LoadStationAsync(environment.PlatformCoreConnectionString, "UNLOAD_01");
    var shuttle = await LoadDeviceShadowAsync(environment.PlatformCoreConnectionString, "SHUTTLE_01");
    var journal = await LoadJournalAsync(environment.PlatformCoreConnectionString, "corr-station-unload-01");
    var journalEvent = Assert.Single(journal);

    Assert.Equal(ExecutionTaskState.Completed, runtime.State);
    Assert.Equal("payload-unload-01", station.CurrentPayloadId);
    Assert.Null(shuttle.CarriedPayloadId);
    Assert.Equal(DeviceExecutionState.Idle, shuttle.ExecutionState);
    Assert.Equal(DispatchStatus.Available, shuttle.DispatchStatus);
    Assert.Equal("PayloadCustodyChanged", journalEvent.EventName);
  }

  private static SubmitExecutionTask CreateLoadSubmitCommand() =>
      new(
          new ContractEnvelope(
              new EnvelopeId("msg-station-load-01"),
              new CorrelationId("corr-station-load-01")),
          new ExecutionTaskId("task-station-load-01"),
          new TaskRevision(1),
          new JobId("job-station-load-01"),
          ExecutionTaskType.StationTransfer,
          ExecutionResourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
          [ExecutionResourceRef.ForStationBoundary(new StationId("LOAD_01"))],
          targetNode: new NodeId("L1_LOAD_01"));

  private static SubmitExecutionTask CreateUnloadSubmitCommand() =>
      new(
          new ContractEnvelope(
              new EnvelopeId("msg-station-unload-01"),
              new CorrelationId("corr-station-unload-01")),
          new ExecutionTaskId("task-station-unload-01"),
          new TaskRevision(1),
          new JobId("job-station-unload-01"),
          ExecutionTaskType.StationTransfer,
          ExecutionResourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
          [ExecutionResourceRef.ForStationBoundary(new StationId("UNLOAD_01"))],
          targetNode: new NodeId("L2_UNLOAD_01"));

  private static async Task SetStationStateAsync(
      string connectionString,
      string stationId,
      StationReadiness readiness,
      string? currentPayloadId)
  {
    await using var context = CreateContext(connectionString);
    var station = await context.Set<StationBoundaryStateRecord>()
        .SingleAsync(record => record.StationId == stationId);
    station.Readiness = readiness;
    station.CurrentPayloadId = currentPayloadId;
    station.LastUpdatedAt = DateTimeOffset.UtcNow;
    await context.SaveChangesAsync();
  }

  private static async Task SetShuttleStateAsync(
      string connectionString,
      string currentNodeId,
      string? carriedPayloadId,
      DispatchStatus dispatchStatus,
      DeviceExecutionState executionState)
  {
    await using var context = CreateContext(connectionString);
    var shuttle = await context.Set<DeviceShadowRecord>()
        .SingleAsync(record => record.DeviceId == "SHUTTLE_01");
    shuttle.CurrentNodeId = currentNodeId;
    shuttle.CarriedPayloadId = carriedPayloadId;
    shuttle.DispatchStatus = dispatchStatus;
    shuttle.ExecutionState = executionState;
    shuttle.LastObservedAt = DateTimeOffset.UtcNow;
    await context.SaveChangesAsync();
  }

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
        .SingleAsync(record => record.OwnerType == "ExecutionTask" && record.OwnerId == executionTaskId);
  }

  private static async Task<StationBoundaryStateRecord> LoadStationAsync(string connectionString, string stationId)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<StationBoundaryStateRecord>()
        .AsNoTracking()
        .SingleAsync(record => record.StationId == stationId);
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

  private static async Task<IReadOnlyList<PlatformEventJournalRecord>> LoadJournalAsync(string connectionString, string correlationId)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<PlatformEventJournalRecord>()
        .AsNoTracking()
        .Where(record => record.CorrelationId == correlationId)
        .OrderBy(record => record.OccurredAt)
        .ThenBy(record => record.EventId)
        .ToListAsync();
  }

  private static ServiceProvider CreateServiceProvider(string connectionString)
  {
    var services = new ServiceCollection();
    services.AddPlatformCorePersistence(connectionString);
    services.AddWarehouseTopologyServices();
    services.AddSingleton(CreateCompiledTopology());
    services.AddPersistenceWcsOperationalStateStore();
    services.AddPersistenceWcsStationTransferTaskMaterialization();

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
