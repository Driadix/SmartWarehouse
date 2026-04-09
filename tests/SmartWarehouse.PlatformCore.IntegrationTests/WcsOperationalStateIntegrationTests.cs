using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Application.Wcs;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Devices;
using SmartWarehouse.PlatformCore.Domain.Operations;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Domain.Stations;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;
using SmartWarehouse.PlatformCore.Infrastructure.Wcs;

namespace SmartWarehouse.PlatformCore.IntegrationTests;

[Collection(PlatformCoreIntegrationFixtureDefinition.Name)]
public sealed class WcsOperationalStateIntegrationTests
{
  private static readonly string[] CommonCapabilities =
  [
      "event.fault",
      "event.nodeReached",
      "execution.suspendResume",
      "session.lease",
      "snapshot.state"
  ];

  private static readonly string[] ShuttleCapabilities =
  [
      .. CommonCapabilities,
      "mode.carrierPassenger",
      "motion.windowed",
      "transfer.lift.hybridPassenger",
      "transfer.station.passive"
  ];

  private static readonly string[] HybridLiftCapabilities =
  [
      .. CommonCapabilities,
      "motion.vertical.singleSlot",
      "occupancy.singleShuttle",
      "transfer.lift.dispatchShuttle",
      "transfer.lift.receiveShuttle"
  ];

  private readonly PlatformCoreTestcontainersHarness _harness;

  public WcsOperationalStateIntegrationTests(PlatformCoreTestcontainersHarness harness)
  {
    _harness = harness;
  }

  [Fact]
  public async Task EnsureInitializedSeedsTopologyDerivedDeviceAndStationStateIdempotently()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();

    await store.EnsureInitializedAsync();

    var seededDeviceShadows = await LoadDeviceShadowsAsync(environment.PlatformCoreConnectionString);
    var seededStationStates = await LoadStationStatesAsync(environment.PlatformCoreConnectionString);

    var deviceShadowsById = seededDeviceShadows.ToDictionary(static record => record.DeviceId, StringComparer.Ordinal);
    Assert.True(deviceShadowsById.TryGetValue("SHUTTLE_01", out var shuttle));
    Assert.Equal(DeviceFamily.Shuttle3D, shuttle.DeviceFamily);
    Assert.Equal("L1_TRAVEL_001", shuttle.CurrentNodeId);
    Assert.Equal("HEALTHY", shuttle.HealthState);
    Assert.Equal(DeviceExecutionState.Idle, shuttle.ExecutionState);
    Assert.Equal(
        ShuttleCapabilities.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
        shuttle.StaticCapabilities.OrderBy(static value => value, StringComparer.Ordinal).ToArray());
    Assert.Equal(
        ShuttleCapabilities.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
        shuttle.ActiveCapabilities.OrderBy(static value => value, StringComparer.Ordinal).ToArray());
    Assert.Equal(nameof(ShuttleMovementMode.Autonomous), shuttle.MovementMode);
    Assert.Equal(DispatchStatus.Available, shuttle.DispatchStatus);
    Assert.Null(shuttle.CarrierId);
    Assert.Null(shuttle.CarriedPayloadId);
    Assert.Null(shuttle.CarrierKind);
    Assert.Null(shuttle.SlotCount);
    Assert.Null(shuttle.OccupiedShuttleId);

    Assert.True(deviceShadowsById.TryGetValue("LIFT_A_DEVICE", out var lift));
    Assert.Equal(DeviceFamily.HybridLift, lift.DeviceFamily);
    Assert.Equal("L1_CARRIER_A", lift.CurrentNodeId);
    Assert.Equal("HEALTHY", lift.HealthState);
    Assert.Equal(DeviceExecutionState.Idle, lift.ExecutionState);
    Assert.Equal(
        HybridLiftCapabilities.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
        lift.StaticCapabilities.OrderBy(static value => value, StringComparer.Ordinal).ToArray());
    Assert.Equal(
        HybridLiftCapabilities.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
        lift.ActiveCapabilities.OrderBy(static value => value, StringComparer.Ordinal).ToArray());
    Assert.Null(lift.MovementMode);
    Assert.Null(lift.DispatchStatus);
    Assert.Equal(CarrierKind.HybridLift, lift.CarrierKind);
    Assert.Equal(1, lift.SlotCount);
    Assert.Null(lift.OccupiedShuttleId);

    Assert.Collection(
        seededStationStates.OrderBy(static record => record.StationId, StringComparer.Ordinal),
        load =>
        {
          Assert.Equal("LOAD_01", load.StationId);
          Assert.Equal(StationType.Load, load.StationType);
          Assert.Equal("L1_LOAD_01", load.AttachedNodeId);
          Assert.Equal(StationControlMode.Passive, load.ControlMode);
          Assert.Equal(StationReadiness.Ready, load.Readiness);
          Assert.Equal(1, load.BufferCapacity);
          Assert.Null(load.CurrentPayloadId);
        },
        unload =>
        {
          Assert.Equal("UNLOAD_01", unload.StationId);
          Assert.Equal(StationType.Unload, unload.StationType);
          Assert.Equal("L2_UNLOAD_01", unload.AttachedNodeId);
          Assert.Equal(StationControlMode.Passive, unload.ControlMode);
          Assert.Equal(StationReadiness.Ready, unload.Readiness);
          Assert.Equal(1, unload.BufferCapacity);
          Assert.Null(unload.CurrentPayloadId);
        });

    var initialDeviceTimestamps = seededDeviceShadows.ToDictionary(static record => record.DeviceId, static record => record.LastObservedAt);
    var initialStationTimestamps = seededStationStates.ToDictionary(static record => record.StationId, static record => record.LastUpdatedAt);

    await Task.Delay(TimeSpan.FromMilliseconds(20));
    await store.EnsureInitializedAsync();

    var repeatedDeviceShadows = await LoadDeviceShadowsAsync(environment.PlatformCoreConnectionString);
    var repeatedStationStates = await LoadStationStatesAsync(environment.PlatformCoreConnectionString);

    Assert.Equal(2, repeatedDeviceShadows.Count);
    Assert.Equal(2, repeatedStationStates.Count);
    foreach (var deviceShadow in repeatedDeviceShadows)
    {
      Assert.Equal(initialDeviceTimestamps[deviceShadow.DeviceId], deviceShadow.LastObservedAt);
    }

    foreach (var stationState in repeatedStationStates)
    {
      Assert.Equal(initialStationTimestamps[stationState.StationId], stationState.LastUpdatedAt);
    }
  }

  [Fact]
  public async Task StoreRoundTripsOperationalStateWithoutMixingWithWesSchema()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var store = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();
    await store.EnsureInitializedAsync();

    var updatedShuttle = new DeviceShadowState(
        new Shuttle3D(
            new DeviceId("SHUTTLE_01"),
            new NodeId("L1_SWITCH_A"),
            new DeviceHealthState("HEALTHY"),
            CreateCapabilitySet(ShuttleCapabilities),
            DeviceExecutionState.Executing,
            ShuttleMovementMode.Autonomous,
            DispatchStatus.Occupied,
            carrierId: null,
            carriedPayloadId: new PayloadId("PALLET-01")),
        DateTimeOffset.UtcNow);
    var updatedStation = new StationBoundaryStateSnapshot(
        new LoadStation(new StationId("LOAD_01"), new NodeId("L1_LOAD_01"), StationReadiness.Blocked, bufferCapacity: 1),
        new PayloadId("PALLET-01"),
        DateTimeOffset.UtcNow);
    var replacementSession = new DeviceSession(
        new DeviceSessionId("session-02"),
        new DeviceId("SHUTTLE_01"),
        new DeviceSessionState("LEASED"),
        leaseUntil: DateTimeOffset.UtcNow.AddMinutes(10),
        lastHeartbeatAt: DateTimeOffset.UtcNow.AddMinutes(5));
    var initialSession = new DeviceSession(
        new DeviceSessionId("session-01"),
        new DeviceId("SHUTTLE_01"),
        new DeviceSessionState("LEASED"),
        leaseUntil: DateTimeOffset.UtcNow.AddMinutes(5),
        lastHeartbeatAt: DateTimeOffset.UtcNow);
    var fault = new Fault(
        new FaultId("fault-01"),
        FaultSourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
        new FaultCode("DRIVE_TIMEOUT"),
        new FaultSeverity("MAJOR"),
        FaultState.Active);
    var reservation = new Reservation(
        new ReservationId("reservation-01"),
        ReservationOwnerRef.ForExecutionTask(new ExecutionTaskId("task-01")),
        [new NodeId("L1_SWITCH_A"), new NodeId("L1_TP_LIFT_A")],
        ReservationHorizon.Execution,
        new ReservationState("ACTIVE"));

    await store.UpsertDeviceShadowAsync(updatedShuttle);
    await store.UpsertStationStateAsync(updatedStation);
    await store.UpsertDeviceSessionAsync(initialSession);
    await store.UpsertDeviceSessionAsync(replacementSession);
    await store.UpsertFaultAsync(fault);
    await store.UpsertReservationAsync(reservation);

    var persistedShuttle = Assert.IsType<Shuttle3D>((await store.FindDeviceShadowAsync(new DeviceId("SHUTTLE_01")))!.Device);
    var persistedStation = await store.FindStationStateAsync(new StationId("LOAD_01"));
    var persistedSession = await store.FindDeviceSessionAsync(new DeviceId("SHUTTLE_01"));
    var persistedFault = await store.FindFaultAsync(new FaultId("fault-01"));
    var persistedReservation = await store.FindReservationAsync(new ReservationId("reservation-01"));
    var sourceFaults = await store.ListFaultsBySourceAsync(FaultSourceRef.ForDevice(new DeviceId("SHUTTLE_01")));
    var ownerReservations = await store.ListReservationsByOwnerAsync(ReservationOwnerRef.ForExecutionTask(new ExecutionTaskId("task-01")));

    Assert.Equal("L1_SWITCH_A", persistedShuttle.CurrentNode?.Value);
    Assert.Equal(DeviceExecutionState.Executing, persistedShuttle.ExecutionState);
    Assert.Equal(DispatchStatus.Occupied, persistedShuttle.DispatchStatus);
    Assert.Equal("PALLET-01", persistedShuttle.CarriedPayloadId?.Value);
    Assert.NotNull(persistedStation);
    Assert.Equal(StationReadiness.Blocked, persistedStation.StationBoundary.Readiness);
    Assert.Equal("PALLET-01", persistedStation.CurrentPayloadId?.Value);
    Assert.NotNull(persistedSession);
    Assert.Equal("session-02", persistedSession.SessionId.Value);
    Assert.Equal("LEASED", persistedSession.State.Value);
    Assert.NotNull(persistedFault);
    Assert.Equal("DRIVE_TIMEOUT", persistedFault.FaultCode.Value);
    Assert.NotNull(persistedReservation);
    Assert.Equal(["L1_SWITCH_A", "L1_TP_LIFT_A"], persistedReservation.Nodes.Select(static node => node.Value).ToArray());
    Assert.Single(sourceFaults);
    Assert.Single(ownerReservations);

    var deviceSessionRecords = await LoadDeviceSessionsAsync(environment.PlatformCoreConnectionString);
    var faultRecords = await LoadFaultsAsync(environment.PlatformCoreConnectionString);
    var reservationRecords = await LoadReservationsAsync(environment.PlatformCoreConnectionString);
    var shuttleShadowRecord = await LoadDeviceShadowAsync(environment.PlatformCoreConnectionString, "SHUTTLE_01");
    var stationStateRecord = await LoadStationStateAsync(environment.PlatformCoreConnectionString, "LOAD_01");

    Assert.Single(deviceSessionRecords);
    Assert.Equal("session-02", deviceSessionRecords[0].DeviceSessionId);
    Assert.Single(faultRecords);
    Assert.Equal("SHUTTLE_01", faultRecords[0].SourceId);
    Assert.Single(reservationRecords);
    Assert.Equal("ExecutionTask", reservationRecords[0].OwnerType);
    Assert.Equal("PALLET-01", shuttleShadowRecord.CarriedPayloadId);
    Assert.Equal("PALLET-01", stationStateRecord.CurrentPayloadId);

    await using var context = CreateContext(environment.PlatformCoreConnectionString);
    Assert.Empty(await context.Set<JobRecord>().AsNoTracking().ToListAsync());
  }

  private static CapabilitySet CreateCapabilitySet(IEnumerable<string> capabilities) =>
      new(
          capabilities.Select(static capability => new CapabilityId(capability)),
          capabilities.Select(static capability => new CapabilityId(capability)));

  private static async Task ApplyMigrationsAsync(string connectionString)
  {
    await using var context = CreateContext(connectionString);
    await context.Database.MigrateAsync();
  }

  private static async Task<List<DeviceShadowRecord>> LoadDeviceShadowsAsync(string connectionString)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<DeviceShadowRecord>().AsNoTracking().ToListAsync();
  }

  private static async Task<DeviceShadowRecord> LoadDeviceShadowAsync(string connectionString, string deviceId)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<DeviceShadowRecord>()
        .AsNoTracking()
        .SingleAsync(record => record.DeviceId == deviceId);
  }

  private static async Task<List<StationBoundaryStateRecord>> LoadStationStatesAsync(string connectionString)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<StationBoundaryStateRecord>().AsNoTracking().ToListAsync();
  }

  private static async Task<StationBoundaryStateRecord> LoadStationStateAsync(string connectionString, string stationId)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<StationBoundaryStateRecord>()
        .AsNoTracking()
        .SingleAsync(record => record.StationId == stationId);
  }

  private static async Task<List<DeviceSessionRecord>> LoadDeviceSessionsAsync(string connectionString)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<DeviceSessionRecord>().AsNoTracking().OrderBy(record => record.DeviceSessionId).ToListAsync();
  }

  private static async Task<List<FaultRecord>> LoadFaultsAsync(string connectionString)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<FaultRecord>().AsNoTracking().OrderBy(record => record.FaultId).ToListAsync();
  }

  private static async Task<List<ReservationRecord>> LoadReservationsAsync(string connectionString)
  {
    await using var context = CreateContext(connectionString);
    return await context.Set<ReservationRecord>().AsNoTracking().OrderBy(record => record.ReservationId).ToListAsync();
  }

  private static ServiceProvider CreateServiceProvider(string connectionString)
  {
    var services = new ServiceCollection();
    services.AddPlatformCorePersistence(connectionString);
    services.AddWarehouseTopologyServices();
    services.AddSingleton(CreateCompiledTopology());
    services.AddPersistenceWcsOperationalStateStore();

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
