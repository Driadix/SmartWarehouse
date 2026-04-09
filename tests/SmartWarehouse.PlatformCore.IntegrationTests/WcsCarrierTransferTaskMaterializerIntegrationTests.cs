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
public sealed class WcsCarrierTransferTaskMaterializerIntegrationTests
{
  private readonly PlatformCoreTestcontainersHarness _harness;

  public WcsCarrierTransferTaskMaterializerIntegrationTests(PlatformCoreTestcontainersHarness harness)
  {
    _harness = harness;
  }

  [Fact]
  public async Task MaterializeIssuesPrepareTransferIdempotentlyAndThenMoveCarrierAfterBoardingIsConfirmed()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();
    var processor = scope.ServiceProvider.GetRequiredService<IWcsExecutionTaskCommandProcessor>();
    var materializer = scope.ServiceProvider.GetRequiredService<IWcsCarrierTransferTaskMaterializer>();

    await store.EnsureInitializedAsync();
    await UpsertSessionsAsync(store);
    await processor.SubmitAsync(CreateSubmitCommand());
    await SetShuttleStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L1_TP_LIFT_A",
        movementMode: ShuttleMovementMode.Autonomous,
        carrierId: null,
        carriedPayloadId: "payload-carrier-01",
        dispatchStatus: DispatchStatus.Occupied,
        executionState: DeviceExecutionState.Idle);
    await SetLiftStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L1_CARRIER_A",
        occupiedShuttleId: null,
        executionState: DeviceExecutionState.Idle);

    var first = await materializer.MaterializeAsync(new ExecutionTaskId("task-carrier-01"));
    var second = await materializer.MaterializeAsync(new ExecutionTaskId("task-carrier-01"));

    await SetShuttleStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L1_CARRIER_A",
        movementMode: ShuttleMovementMode.CarrierPassenger,
        carrierId: "LIFT_A_DEVICE",
        carriedPayloadId: "payload-carrier-01",
        dispatchStatus: DispatchStatus.Occupied,
        executionState: DeviceExecutionState.Executing);
    await SetLiftStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L1_CARRIER_A",
        occupiedShuttleId: "SHUTTLE_01",
        executionState: DeviceExecutionState.Executing);

    var third = await materializer.MaterializeAsync(new ExecutionTaskId("task-carrier-01"));

    Assert.Equal(CarrierTransferMaterializationStatus.CommandsIssued, first.Status);
    Assert.Equal("PrepareTransfer", first.RuntimePhase);
    Assert.Equal(2, first.OutboxIds.Count);

    Assert.Equal(CarrierTransferMaterializationStatus.AwaitingConfirmation, second.Status);
    Assert.Equal("PrepareTransfer", second.RuntimePhase);
    Assert.Equal(2, second.OutboxIds.Count);

    Assert.Equal(CarrierTransferMaterializationStatus.CommandsIssued, third.Status);
    Assert.Equal("MoveCarrier", third.RuntimePhase);
    Assert.Single(third.OutboxIds);

    var runtime = await LoadRuntimeAsync(environment.PlatformCoreConnectionString, "task-carrier-01");
    var reservation = await LoadReservationAsync(environment.PlatformCoreConnectionString, "task-carrier-01");
    var outboxMessages = await LoadTaskOutboxMessagesAsync(environment.PlatformCoreConnectionString, "task-carrier-01");
    var journal = await LoadJournalAsync(environment.PlatformCoreConnectionString, "corr-carrier-01");

    Assert.Equal(ExecutionTaskState.InProgress, runtime.State);
    Assert.Equal("MoveCarrier", runtime.ActiveRuntimePhase);
    Assert.Equal("ACTIVE", reservation.State);
    Assert.Equal(
        ["L1_TP_LIFT_A", "L1_CARRIER_A", "L2_CARRIER_A", "L2_TP_LIFT_A"],
        reservation.ReservedNodeIds);
    Assert.Empty(journal);

    Assert.Equal(3, outboxMessages.Count);
    Assert.All(outboxMessages, message => Assert.Equal("INTERNAL_COMMAND", message.MessageKind));

    var shuttlePrepare = Assert.Single(outboxMessages, message => HasMessageType(message.Payload, "PrepareTransfer") && HasDevice(message.Payload, "SHUTTLE_01"));
    var liftPrepare = Assert.Single(outboxMessages, message => HasMessageType(message.Payload, "PrepareTransfer") && HasDevice(message.Payload, "LIFT_A_DEVICE"));
    var moveCarrier = Assert.Single(outboxMessages, message => HasMessageType(message.Payload, "MoveCarrier"));

    using (var shuttlePrepareDocument = JsonDocument.Parse(shuttlePrepare.Payload))
    {
      var payload = shuttlePrepareDocument.RootElement.GetProperty("payload");
      Assert.Equal("L1_TP_LIFT_A", payload.GetProperty("transferPointId").GetString());
      Assert.Equal("SOURCE", payload.GetProperty("role").GetString());
    }

    using (var liftPrepareDocument = JsonDocument.Parse(liftPrepare.Payload))
    {
      var payload = liftPrepareDocument.RootElement.GetProperty("payload");
      Assert.Equal("L1_TP_LIFT_A", payload.GetProperty("transferPointId").GetString());
      Assert.Equal("RECEIVER", payload.GetProperty("role").GetString());
    }

    using (var moveCarrierDocument = JsonDocument.Parse(moveCarrier.Payload))
    {
      var payload = moveCarrierDocument.RootElement.GetProperty("payload");
      Assert.Equal("L2_CARRIER_A", payload.GetProperty("targetCarrierNode").GetString());
    }
  }

  [Fact]
  public async Task MaterializeWaitsForConfirmedExitBeforeCompletingCarrierTransfer()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();
    var processor = scope.ServiceProvider.GetRequiredService<IWcsExecutionTaskCommandProcessor>();
    var materializer = scope.ServiceProvider.GetRequiredService<IWcsCarrierTransferTaskMaterializer>();

    await store.EnsureInitializedAsync();
    await UpsertSessionsAsync(store);
    await processor.SubmitAsync(CreateSubmitCommand());
    await SetShuttleStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L2_CARRIER_A",
        movementMode: ShuttleMovementMode.CarrierPassenger,
        carrierId: "LIFT_A_DEVICE",
        carriedPayloadId: "payload-carrier-02",
        dispatchStatus: DispatchStatus.Occupied,
        executionState: DeviceExecutionState.Executing);
    await SetLiftStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L2_CARRIER_A",
        occupiedShuttleId: "SHUTTLE_01",
        executionState: DeviceExecutionState.Executing);

    var result = await materializer.MaterializeAsync(new ExecutionTaskId("task-carrier-01"));

    Assert.Equal(CarrierTransferMaterializationStatus.AwaitingConfirmation, result.Status);
    Assert.Equal("ExitCarrier", result.RuntimePhase);
    Assert.Empty(result.OutboxIds);

    var runtime = await LoadRuntimeAsync(environment.PlatformCoreConnectionString, "task-carrier-01");
    var outboxMessages = await LoadTaskOutboxMessagesAsync(environment.PlatformCoreConnectionString, "task-carrier-01");
    var journal = await LoadJournalAsync(environment.PlatformCoreConnectionString, "corr-carrier-01");

    Assert.Equal(ExecutionTaskState.InProgress, runtime.State);
    Assert.Equal("ExitCarrier", runtime.ActiveRuntimePhase);
    Assert.Empty(outboxMessages);
    Assert.Empty(journal);
  }

  [Fact]
  public async Task MaterializeCompletesCarrierTransferAndPublishesTransferCommitted()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();
    var processor = scope.ServiceProvider.GetRequiredService<IWcsExecutionTaskCommandProcessor>();
    var materializer = scope.ServiceProvider.GetRequiredService<IWcsCarrierTransferTaskMaterializer>();

    await store.EnsureInitializedAsync();
    await UpsertSessionsAsync(store);
    await processor.SubmitAsync(CreateSubmitCommand());
    await SetShuttleStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L1_TP_LIFT_A",
        movementMode: ShuttleMovementMode.Autonomous,
        carrierId: null,
        carriedPayloadId: "payload-carrier-03",
        dispatchStatus: DispatchStatus.Occupied,
        executionState: DeviceExecutionState.Idle);
    await SetLiftStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L1_CARRIER_A",
        occupiedShuttleId: null,
        executionState: DeviceExecutionState.Idle);

    await materializer.MaterializeAsync(new ExecutionTaskId("task-carrier-01"));

    await SetShuttleStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L1_CARRIER_A",
        movementMode: ShuttleMovementMode.CarrierPassenger,
        carrierId: "LIFT_A_DEVICE",
        carriedPayloadId: "payload-carrier-03",
        dispatchStatus: DispatchStatus.Occupied,
        executionState: DeviceExecutionState.Executing);
    await SetLiftStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L1_CARRIER_A",
        occupiedShuttleId: "SHUTTLE_01",
        executionState: DeviceExecutionState.Executing);

    await materializer.MaterializeAsync(new ExecutionTaskId("task-carrier-01"));

    await SetShuttleStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L2_TP_LIFT_A",
        movementMode: ShuttleMovementMode.Autonomous,
        carrierId: null,
        carriedPayloadId: "payload-carrier-03",
        dispatchStatus: DispatchStatus.Occupied,
        executionState: DeviceExecutionState.Executing);
    await SetLiftStateAsync(
        environment.PlatformCoreConnectionString,
        currentNodeId: "L2_CARRIER_A",
        occupiedShuttleId: null,
        executionState: DeviceExecutionState.Executing);

    var completed = await materializer.MaterializeAsync(new ExecutionTaskId("task-carrier-01"));

    Assert.Equal(CarrierTransferMaterializationStatus.Completed, completed.Status);
    Assert.Equal("Completed", completed.RuntimePhase);
    Assert.Equal(3, completed.OutboxIds.Count);

    var runtime = await LoadRuntimeAsync(environment.PlatformCoreConnectionString, "task-carrier-01");
    var reservation = await LoadReservationAsync(environment.PlatformCoreConnectionString, "task-carrier-01");
    var shuttle = await LoadDeviceShadowAsync(environment.PlatformCoreConnectionString, "SHUTTLE_01");
    var lift = await LoadDeviceShadowAsync(environment.PlatformCoreConnectionString, "LIFT_A_DEVICE");
    var outboxMessages = await LoadTaskOutboxMessagesAsync(environment.PlatformCoreConnectionString, "task-carrier-01");
    var journal = await LoadJournalAsync(environment.PlatformCoreConnectionString, "corr-carrier-01");
    var platformEvent = Assert.Single(outboxMessages, message => message.MessageKind == "PLATFORM_EVENT");
    var journalEvent = Assert.Single(journal);

    Assert.Equal(ExecutionTaskState.Completed, runtime.State);
    Assert.Equal("Completed", runtime.ActiveRuntimePhase);
    Assert.Equal("RELEASED", reservation.State);

    Assert.Equal("L2_TP_LIFT_A", shuttle.CurrentNodeId);
    Assert.Equal(nameof(ShuttleMovementMode.Autonomous), shuttle.MovementMode);
    Assert.Null(shuttle.CarrierId);
    Assert.Equal("payload-carrier-03", shuttle.CarriedPayloadId);
    Assert.Equal(DeviceExecutionState.Idle, shuttle.ExecutionState);
    Assert.Equal(DispatchStatus.Occupied, shuttle.DispatchStatus);

    Assert.Equal("L2_CARRIER_A", lift.CurrentNodeId);
    Assert.Null(lift.OccupiedShuttleId);
    Assert.Equal(DeviceExecutionState.Idle, lift.ExecutionState);

    Assert.Equal(6, outboxMessages.Count);
    Assert.Equal("TransferCommitted", journalEvent.EventName);
    Assert.Equal(PlatformEventVisibility.Operations, journalEvent.Visibility);

    var shuttleCommit = Assert.Single(outboxMessages, message => HasMessageType(message.Payload, "CommitTransfer") && HasDevice(message.Payload, "SHUTTLE_01"));
    var liftCommit = Assert.Single(outboxMessages, message => HasMessageType(message.Payload, "CommitTransfer") && HasDevice(message.Payload, "LIFT_A_DEVICE"));

    using (var shuttleCommitDocument = JsonDocument.Parse(shuttleCommit.Payload))
    {
      var payload = shuttleCommitDocument.RootElement.GetProperty("payload");
      Assert.Equal("L2_TP_LIFT_A", payload.GetProperty("transferPointId").GetString());
    }

    using (var liftCommitDocument = JsonDocument.Parse(liftCommit.Payload))
    {
      var payload = liftCommitDocument.RootElement.GetProperty("payload");
      Assert.Equal("L2_TP_LIFT_A", payload.GetProperty("transferPointId").GetString());
    }

    using (var eventDocument = JsonDocument.Parse(platformEvent.Payload))
    {
      var root = eventDocument.RootElement;
      Assert.Equal("TransferCommitted", root.GetProperty("eventName").GetString());
      Assert.Equal("corr-carrier-01", root.GetProperty("correlationId").GetString());
      Assert.Equal("task-carrier-01", root.GetProperty("causationId").GetString());
      var payload = root.GetProperty("payload");
      Assert.Equal("task-carrier-01", payload.GetProperty("executionTaskId").GetString());
      Assert.Equal("ShuttleRidesHybridLiftWithPayload", payload.GetProperty("transferMode").GetString());
      Assert.Equal("L2_TP_LIFT_A", payload.GetProperty("transferPointId").GetString());
      var participants = payload.GetProperty("participants").EnumerateArray().Select(static entry => (
          Type: entry.GetProperty("participantType").GetString(),
          Id: entry.GetProperty("participantId").GetString())).ToArray();
      Assert.Equal(
          [("Device", "SHUTTLE_01"), ("Device", "LIFT_A_DEVICE")],
          participants);
    }
  }

  private static SubmitExecutionTask CreateSubmitCommand() =>
      new(
          new ContractEnvelope(
              new EnvelopeId("msg-carrier-01"),
              new CorrelationId("corr-carrier-01")),
          new ExecutionTaskId("task-carrier-01"),
          new TaskRevision(1),
          new JobId("job-carrier-01"),
          ExecutionTaskType.CarrierTransfer,
          ExecutionResourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
          [ExecutionResourceRef.ForDevice(new DeviceId("LIFT_A_DEVICE"))],
          sourceNode: new NodeId("L1_TP_LIFT_A"),
          targetNode: new NodeId("L2_TP_LIFT_A"),
          transferMode: TransferMode.ShuttleRidesHybridLiftWithPayload);

  private static async Task UpsertSessionsAsync(IWcsOperationalStateStore store)
  {
    await store.UpsertDeviceSessionAsync(
        new DeviceSession(
            new DeviceSessionId("session-shuttle-01"),
            new DeviceId("SHUTTLE_01"),
            new DeviceSessionState("LEASED"),
            leaseUntil: DateTimeOffset.UtcNow.AddMinutes(5),
            lastHeartbeatAt: DateTimeOffset.UtcNow));
    await store.UpsertDeviceSessionAsync(
        new DeviceSession(
            new DeviceSessionId("session-lift-01"),
            new DeviceId("LIFT_A_DEVICE"),
            new DeviceSessionState("LEASED"),
            leaseUntil: DateTimeOffset.UtcNow.AddMinutes(5),
            lastHeartbeatAt: DateTimeOffset.UtcNow));
  }

  private static async Task SetShuttleStateAsync(
      string connectionString,
      string currentNodeId,
      ShuttleMovementMode movementMode,
      string? carrierId,
      string? carriedPayloadId,
      DispatchStatus dispatchStatus,
      DeviceExecutionState executionState)
  {
    await using var context = CreateContext(connectionString);
    var shuttle = await context.Set<DeviceShadowRecord>()
        .SingleAsync(record => record.DeviceId == "SHUTTLE_01");
    shuttle.CurrentNodeId = currentNodeId;
    shuttle.MovementMode = movementMode.ToString();
    shuttle.CarrierId = carrierId;
    shuttle.CarriedPayloadId = carriedPayloadId;
    shuttle.DispatchStatus = dispatchStatus;
    shuttle.ExecutionState = executionState;
    shuttle.LastObservedAt = DateTimeOffset.UtcNow;
    await context.SaveChangesAsync();
  }

  private static async Task SetLiftStateAsync(
      string connectionString,
      string currentNodeId,
      string? occupiedShuttleId,
      DeviceExecutionState executionState)
  {
    await using var context = CreateContext(connectionString);
    var lift = await context.Set<DeviceShadowRecord>()
        .SingleAsync(record => record.DeviceId == "LIFT_A_DEVICE");
    lift.CurrentNodeId = currentNodeId;
    lift.OccupiedShuttleId = occupiedShuttleId;
    lift.ExecutionState = executionState;
    lift.LastObservedAt = DateTimeOffset.UtcNow;
    await context.SaveChangesAsync();
  }

  private static bool HasMessageType(string payload, string messageType)
  {
    using var document = JsonDocument.Parse(payload);
    return document.RootElement.TryGetProperty("messageType", out var property) &&
           string.Equals(property.GetString(), messageType, StringComparison.Ordinal);
  }

  private static bool HasDevice(string payload, string deviceId)
  {
    using var document = JsonDocument.Parse(payload);
    return document.RootElement.TryGetProperty("deviceId", out var property) &&
           string.Equals(property.GetString(), deviceId, StringComparison.Ordinal);
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
    services.AddPersistenceWcsCarrierTransferTaskMaterialization();

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
