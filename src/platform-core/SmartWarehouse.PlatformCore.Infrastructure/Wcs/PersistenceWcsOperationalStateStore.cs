using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Application.Wcs;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Devices;
using SmartWarehouse.PlatformCore.Domain.Operations;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Domain.Stations;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

namespace SmartWarehouse.PlatformCore.Infrastructure.Wcs;

public static class PersistenceWcsOperationalStateStoreServiceCollectionExtensions
{
  public static IServiceCollection AddPersistenceWcsOperationalStateStore(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddPersistenceWcsExecutionTaskCommandProcessing();
    services.AddScoped<IWcsOperationalStateStore, PersistenceWcsOperationalStateStore>();
    services.AddHostedService<WcsOperationalStateInitializationHostedService>();

    return services;
  }
}

internal sealed class PersistenceWcsOperationalStateStore(
    PlatformCoreDbContext dbContext,
    CompiledWarehouseTopology topology) : IWcsOperationalStateStore
{
  private static readonly string[] CommonActiveCapabilities =
  [
      "session.lease",
      "snapshot.state",
      "event.nodeReached",
      "event.fault",
      "execution.suspendResume"
  ];

  private static readonly string[] ShuttleCapabilities =
  [
      .. CommonActiveCapabilities,
      "motion.windowed",
      "transfer.station.passive",
      "transfer.lift.hybridPassenger",
      "mode.carrierPassenger"
  ];

  private static readonly string[] HybridLiftCapabilities =
  [
      .. CommonActiveCapabilities,
      "motion.vertical.singleSlot",
      "transfer.lift.receiveShuttle",
      "transfer.lift.dispatchShuttle",
      "occupancy.singleShuttle"
  ];

  public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
  {
    var now = DateTimeOffset.UtcNow;
    var knownDeviceIds = await dbContext.DeviceShadows
        .AsNoTracking()
        .Select(record => record.DeviceId)
        .ToListAsync(cancellationToken);
    var knownStationIds = await dbContext.StationBoundaryStates
        .AsNoTracking()
        .Select(record => record.StationId)
        .ToListAsync(cancellationToken);

    foreach (var deviceBinding in topology.DeviceBindings.Where(binding => !knownDeviceIds.Contains(binding.DeviceId.Value, StringComparer.Ordinal)))
    {
      dbContext.DeviceShadows.Add(CreateDeviceShadowRecord(deviceBinding, now));
    }

    foreach (var station in topology.Stations.Where(station => !knownStationIds.Contains(station.StationId.Value, StringComparer.Ordinal)))
    {
      dbContext.StationBoundaryStates.Add(new StationBoundaryStateRecord
      {
        StationId = station.StationId.Value,
        StationType = station.StationType,
        AttachedNodeId = station.AttachedNodeId.Value,
        ControlMode = station.ControlMode,
        Readiness = StationReadiness.Ready,
        BufferCapacity = station.BufferCapacity,
        CurrentPayloadId = null,
        LastUpdatedAt = now
      });
    }

    if (!dbContext.ChangeTracker.HasChanges())
    {
      return;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<DeviceShadowState>> ListDeviceShadowsAsync(CancellationToken cancellationToken = default)
  {
    var records = await dbContext.DeviceShadows
          .AsNoTracking()
          .OrderBy(record => record.DeviceId)
          .ToListAsync(cancellationToken);
    return records.Select(MapDeviceShadow).ToArray();
  }

  public async Task<DeviceShadowState?> FindDeviceShadowAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
  {
    var record = await dbContext.DeviceShadows
        .AsNoTracking()
        .SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId.Value, cancellationToken);

    return record is null ? null : MapDeviceShadow(record);
  }

  public async Task UpsertDeviceShadowAsync(DeviceShadowState deviceShadow, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(deviceShadow);

    var device = deviceShadow.Device;
    var record = await dbContext.DeviceShadows
        .SingleOrDefaultAsync(candidate => candidate.DeviceId == device.DeviceId.Value, cancellationToken);

    if (record is null)
    {
      dbContext.DeviceShadows.Add(CreateDeviceShadowRecord(deviceShadow));
    }
    else
    {
      ApplyDeviceShadowRecord(record, deviceShadow);
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<StationBoundaryStateSnapshot>> ListStationStatesAsync(CancellationToken cancellationToken = default)
  {
    var records = await dbContext.StationBoundaryStates
          .AsNoTracking()
          .OrderBy(record => record.StationId)
          .ToListAsync(cancellationToken);
    return records.Select(MapStationState).ToArray();
  }

  public async Task<StationBoundaryStateSnapshot?> FindStationStateAsync(StationId stationId, CancellationToken cancellationToken = default)
  {
    var record = await dbContext.StationBoundaryStates
        .AsNoTracking()
        .SingleOrDefaultAsync(candidate => candidate.StationId == stationId.Value, cancellationToken);

    return record is null ? null : MapStationState(record);
  }

  public async Task UpsertStationStateAsync(StationBoundaryStateSnapshot stationState, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(stationState);

    var stationId = stationState.StationBoundary.StationId.Value;
    var record = await dbContext.StationBoundaryStates
        .SingleOrDefaultAsync(candidate => candidate.StationId == stationId, cancellationToken);

    if (record is null)
    {
      dbContext.StationBoundaryStates.Add(CreateStationStateRecord(stationState));
    }
    else
    {
      ApplyStationStateRecord(record, stationState);
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  public async Task<DeviceSession?> FindDeviceSessionAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
  {
    var record = await dbContext.DeviceSessions
        .AsNoTracking()
        .SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId.Value, cancellationToken);

    return record is null ? null : MapDeviceSession(record);
  }

  public async Task UpsertDeviceSessionAsync(DeviceSession session, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(session);

    var record = await dbContext.DeviceSessions
        .SingleOrDefaultAsync(
            candidate => candidate.DeviceSessionId == session.SessionId.Value || candidate.DeviceId == session.DeviceId.Value,
            cancellationToken);

    if (record is null)
    {
      dbContext.DeviceSessions.Add(new DeviceSessionRecord
      {
        DeviceSessionId = session.SessionId.Value,
        DeviceId = session.DeviceId.Value,
        State = session.State.Value,
        LeaseUntil = session.LeaseUntil,
        LastHeartbeatAt = session.LastHeartbeatAt
      });
    }
    else
    {
      if (!string.Equals(record.DeviceSessionId, session.SessionId.Value, StringComparison.Ordinal))
      {
        dbContext.DeviceSessions.Remove(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.DeviceSessions.Add(new DeviceSessionRecord
        {
          DeviceSessionId = session.SessionId.Value,
          DeviceId = session.DeviceId.Value,
          State = session.State.Value,
          LeaseUntil = session.LeaseUntil,
          LastHeartbeatAt = session.LastHeartbeatAt
        });
      }
      else
      {
        record.DeviceId = session.DeviceId.Value;
        record.State = session.State.Value;
        record.LeaseUntil = session.LeaseUntil;
        record.LastHeartbeatAt = session.LastHeartbeatAt;
      }
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<Fault>> ListFaultsBySourceAsync(FaultSourceRef source, CancellationToken cancellationToken = default)
  {
    var records = await dbContext.Faults
          .AsNoTracking()
          .Where(record => record.SourceType == source.SourceType.ToString() && record.SourceId == source.SourceId)
          .OrderBy(record => record.FaultId)
          .ToListAsync(cancellationToken);
    return records.Select(MapFault).ToArray();
  }

  public async Task<Fault?> FindFaultAsync(FaultId faultId, CancellationToken cancellationToken = default)
  {
    var record = await dbContext.Faults
        .AsNoTracking()
        .SingleOrDefaultAsync(candidate => candidate.FaultId == faultId.Value, cancellationToken);

    return record is null ? null : MapFault(record);
  }

  public async Task UpsertFaultAsync(Fault fault, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(fault);

    var record = await dbContext.Faults
        .SingleOrDefaultAsync(candidate => candidate.FaultId == fault.FaultId.Value, cancellationToken);

    if (record is null)
    {
      dbContext.Faults.Add(new FaultRecord
      {
        FaultId = fault.FaultId.Value,
        SourceType = fault.Source.SourceType.ToString(),
        SourceId = fault.Source.SourceId,
        FaultCode = fault.FaultCode.Value,
        Severity = fault.Severity.Value,
        State = fault.State
      });
    }
    else
    {
      record.SourceType = fault.Source.SourceType.ToString();
      record.SourceId = fault.Source.SourceId;
      record.FaultCode = fault.FaultCode.Value;
      record.Severity = fault.Severity.Value;
      record.State = fault.State;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<Reservation>> ListReservationsByOwnerAsync(
      ReservationOwnerRef owner,
      CancellationToken cancellationToken = default)
  {
    var records = await dbContext.Reservations
          .AsNoTracking()
          .Where(record => record.OwnerType == owner.OwnerType.ToString() && record.OwnerId == owner.OwnerId)
          .OrderBy(record => record.ReservationId)
          .ToListAsync(cancellationToken);
    return records.Select(MapReservation).ToArray();
  }

  public async Task<Reservation?> FindReservationAsync(ReservationId reservationId, CancellationToken cancellationToken = default)
  {
    var record = await dbContext.Reservations
        .AsNoTracking()
        .SingleOrDefaultAsync(candidate => candidate.ReservationId == reservationId.Value, cancellationToken);

    return record is null ? null : MapReservation(record);
  }

  public async Task UpsertReservationAsync(Reservation reservation, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(reservation);

    var record = await dbContext.Reservations
        .SingleOrDefaultAsync(candidate => candidate.ReservationId == reservation.ReservationId.Value, cancellationToken);

    if (record is null)
    {
      dbContext.Reservations.Add(new ReservationRecord
      {
        ReservationId = reservation.ReservationId.Value,
        OwnerType = reservation.Owner.OwnerType.ToString(),
        OwnerId = reservation.Owner.OwnerId,
        ReservedNodeIds = reservation.Nodes.Select(static node => node.Value).ToArray(),
        Horizon = reservation.Horizon,
        State = reservation.State.Value
      });
    }
    else
    {
      record.OwnerType = reservation.Owner.OwnerType.ToString();
      record.OwnerId = reservation.Owner.OwnerId;
      record.ReservedNodeIds = reservation.Nodes.Select(static node => node.Value).ToArray();
      record.Horizon = reservation.Horizon;
      record.State = reservation.State.Value;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  private DeviceShadowRecord CreateDeviceShadowRecord(CompiledDeviceBinding deviceBinding, DateTimeOffset lastObservedAt)
  {
    var initialNodeId = ResolveInitialNodeId(deviceBinding);
    var capabilities = ResolveCapabilities(deviceBinding.Family);

    return deviceBinding.Family switch
    {
      DeviceFamily.Shuttle3D => new DeviceShadowRecord
      {
        DeviceId = deviceBinding.DeviceId.Value,
        DeviceFamily = deviceBinding.Family,
        CurrentNodeId = initialNodeId,
        HealthState = "HEALTHY",
        ExecutionState = DeviceExecutionState.Idle,
        StaticCapabilities = capabilities,
        ActiveCapabilities = capabilities,
        MovementMode = ShuttleMovementMode.Autonomous.ToString(),
        DispatchStatus = DispatchStatus.Available,
        CarrierId = null,
        CarriedPayloadId = null,
        CarrierKind = null,
        SlotCount = null,
        OccupiedShuttleId = null,
        LastObservedAt = lastObservedAt
      },
      DeviceFamily.HybridLift => new DeviceShadowRecord
      {
        DeviceId = deviceBinding.DeviceId.Value,
        DeviceFamily = deviceBinding.Family,
        CurrentNodeId = initialNodeId,
        HealthState = "HEALTHY",
        ExecutionState = DeviceExecutionState.Idle,
        StaticCapabilities = capabilities,
        ActiveCapabilities = capabilities,
        MovementMode = null,
        DispatchStatus = null,
        CarrierId = null,
        CarriedPayloadId = null,
        CarrierKind = CarrierKind.HybridLift,
        SlotCount = ResolveSlotCount(deviceBinding),
        OccupiedShuttleId = null,
        LastObservedAt = lastObservedAt
      },
      _ => throw new InvalidOperationException($"Unsupported device family '{deviceBinding.Family}'.")
    };
  }

  private static DeviceShadowRecord CreateDeviceShadowRecord(DeviceShadowState deviceShadow)
  {
    var record = new DeviceShadowRecord
    {
      DeviceId = deviceShadow.Device.DeviceId.Value,
      DeviceFamily = deviceShadow.Device.Family
    };

    ApplyDeviceShadowRecord(record, deviceShadow);
    return record;
  }

  private static void ApplyDeviceShadowRecord(DeviceShadowRecord record, DeviceShadowState deviceShadow)
  {
    ArgumentNullException.ThrowIfNull(record);
    ArgumentNullException.ThrowIfNull(deviceShadow);

    var device = deviceShadow.Device;
    record.DeviceFamily = device.Family;
    record.CurrentNodeId = device.CurrentNode?.Value;
    record.HealthState = device.HealthState.Value;
    record.ExecutionState = device.ExecutionState;
    record.StaticCapabilities = device.Capabilities.StaticCapabilities.Select(static capability => capability.Value).ToArray();
    record.ActiveCapabilities = device.ActiveCapabilities.Select(static capability => capability.Value).ToArray();
    record.LastObservedAt = deviceShadow.LastObservedAt;

    switch (device)
    {
      case Shuttle3D shuttle:
        record.MovementMode = shuttle.MovementMode.ToString();
        record.DispatchStatus = shuttle.DispatchStatus;
        record.CarrierId = shuttle.CarrierId?.Value;
        record.CarriedPayloadId = shuttle.CarriedPayloadId?.Value;
        record.CarrierKind = null;
        record.SlotCount = null;
        record.OccupiedShuttleId = null;
        break;
      case HybridLift lift:
        record.MovementMode = null;
        record.DispatchStatus = null;
        record.CarrierId = null;
        record.CarriedPayloadId = null;
        record.CarrierKind = lift.CarrierKind;
        record.SlotCount = lift.SlotCount;
        record.OccupiedShuttleId = lift.OccupiedShuttleId?.Value;
        break;
      default:
        throw new InvalidOperationException($"Unsupported device type '{device.GetType().FullName}'.");
    }
  }

  private static DeviceShadowState MapDeviceShadow(DeviceShadowRecord record)
  {
    ArgumentNullException.ThrowIfNull(record);

    var capabilities = new CapabilitySet(
        record.StaticCapabilities.Select(static capability => new CapabilityId(capability)),
        record.ActiveCapabilities.Select(static capability => new CapabilityId(capability)));
    NodeId? currentNode = string.IsNullOrWhiteSpace(record.CurrentNodeId) ? null : new NodeId(record.CurrentNodeId);
    var device = record.DeviceFamily switch
    {
      DeviceFamily.Shuttle3D => (Device)new Shuttle3D(
          new DeviceId(record.DeviceId),
          currentNode,
          new DeviceHealthState(record.HealthState),
          capabilities,
          record.ExecutionState,
          Enum.Parse<ShuttleMovementMode>(record.MovementMode ?? nameof(ShuttleMovementMode.Autonomous), ignoreCase: false),
          record.DispatchStatus ?? DispatchStatus.Available,
          string.IsNullOrWhiteSpace(record.CarrierId) ? null : new DeviceId(record.CarrierId),
          string.IsNullOrWhiteSpace(record.CarriedPayloadId) ? null : new PayloadId(record.CarriedPayloadId)),
      DeviceFamily.HybridLift => new HybridLift(
          new DeviceId(record.DeviceId),
          currentNode,
          new DeviceHealthState(record.HealthState),
          capabilities,
          record.ExecutionState,
          string.IsNullOrWhiteSpace(record.OccupiedShuttleId) ? null : new DeviceId(record.OccupiedShuttleId)),
      _ => throw new InvalidOperationException($"Unsupported device family '{record.DeviceFamily}'.")
    };

    return new DeviceShadowState(device, record.LastObservedAt);
  }

  private static StationBoundaryStateRecord CreateStationStateRecord(StationBoundaryStateSnapshot stationState)
  {
    var record = new StationBoundaryStateRecord
    {
      StationId = stationState.StationBoundary.StationId.Value
    };

    ApplyStationStateRecord(record, stationState);
    return record;
  }

  private static void ApplyStationStateRecord(StationBoundaryStateRecord record, StationBoundaryStateSnapshot stationState)
  {
    ArgumentNullException.ThrowIfNull(record);
    ArgumentNullException.ThrowIfNull(stationState);

    var station = stationState.StationBoundary;
    record.StationType = station.StationType;
    record.AttachedNodeId = station.AttachedNode.Value;
    record.ControlMode = station.ControlMode;
    record.Readiness = station.Readiness;
    record.BufferCapacity = station.BufferCapacity;
    record.CurrentPayloadId = stationState.CurrentPayloadId?.Value;
    record.LastUpdatedAt = stationState.LastUpdatedAt;
  }

  private static StationBoundaryStateSnapshot MapStationState(StationBoundaryStateRecord record)
  {
    ArgumentNullException.ThrowIfNull(record);

    if (record.ControlMode != StationControlMode.Passive)
    {
      throw new InvalidOperationException(
          $"Station '{record.StationId}' uses unsupported control mode '{record.ControlMode}' in phase 1.");
    }

    var station = record.StationType switch
    {
      StationType.Load => (StationBoundary)new LoadStation(new StationId(record.StationId), new NodeId(record.AttachedNodeId), record.Readiness, record.BufferCapacity),
      StationType.Unload => new UnloadStation(new StationId(record.StationId), new NodeId(record.AttachedNodeId), record.Readiness, record.BufferCapacity),
      _ => throw new InvalidOperationException($"Unsupported station type '{record.StationType}'.")
    };

    return new StationBoundaryStateSnapshot(
        station,
        string.IsNullOrWhiteSpace(record.CurrentPayloadId) ? null : new PayloadId(record.CurrentPayloadId),
        record.LastUpdatedAt);
  }

  private static DeviceSession MapDeviceSession(DeviceSessionRecord record) =>
      new(
          new DeviceSessionId(record.DeviceSessionId),
          new DeviceId(record.DeviceId),
          new DeviceSessionState(record.State),
          record.LeaseUntil,
          record.LastHeartbeatAt);

  private static Fault MapFault(FaultRecord record) =>
      new(
          new FaultId(record.FaultId),
          new FaultSourceRef(Enum.Parse<FaultSourceType>(record.SourceType, ignoreCase: false), record.SourceId),
          new FaultCode(record.FaultCode),
          new FaultSeverity(record.Severity),
          record.State);

  private static Reservation MapReservation(ReservationRecord record) =>
      new(
          new ReservationId(record.ReservationId),
          new ReservationOwnerRef(Enum.Parse<ReservationOwnerType>(record.OwnerType, ignoreCase: false), record.OwnerId),
          record.ReservedNodeIds.Select(static nodeId => new NodeId(nodeId)),
          record.Horizon,
          new ReservationState(record.State));

  private string? ResolveInitialNodeId(CompiledDeviceBinding binding)
  {
    if (binding.InitialNodeId is not null)
    {
      return binding.InitialNodeId.Value;
    }

    if (binding.HomeNodeId is not null)
    {
      return binding.HomeNodeId.Value;
    }

    if (binding.ShaftId is not null && topology.TryGetShaft(binding.ShaftId.Value, out var shaft))
    {
      return shaft.Stops.OrderBy(static stop => stop.LevelOrdinal).First().CarrierNodeId.Value;
    }

    return null;
  }

  private int? ResolveSlotCount(CompiledDeviceBinding binding)
  {
    if (binding.ShaftId is null || !topology.TryGetShaft(binding.ShaftId.Value, out var shaft))
    {
      return 1;
    }

    return shaft.SlotCount;
  }

  private static string[] ResolveCapabilities(DeviceFamily family) =>
      family switch
      {
        DeviceFamily.Shuttle3D => [.. ShuttleCapabilities],
        DeviceFamily.HybridLift => [.. HybridLiftCapabilities],
        _ => throw new InvalidOperationException($"Unsupported device family '{family}'.")
      };
}

internal sealed class WcsOperationalStateInitializationHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    await using var scope = scopeFactory.CreateAsyncScope();
    var stateStore = scope.ServiceProvider.GetRequiredService<IWcsOperationalStateStore>();
    await stateStore.EnsureInitializedAsync(cancellationToken);
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
