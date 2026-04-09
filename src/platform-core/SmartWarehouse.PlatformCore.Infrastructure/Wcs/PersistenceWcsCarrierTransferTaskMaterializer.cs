using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Contracts;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Application.Wcs;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

namespace SmartWarehouse.PlatformCore.Infrastructure.Wcs;

public static class PersistenceWcsCarrierTransferTaskMaterializationServiceCollectionExtensions
{
  public static IServiceCollection AddPersistenceWcsCarrierTransferTaskMaterialization(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddScoped<IWcsCarrierTransferTaskMaterializer, PersistenceWcsCarrierTransferTaskMaterializer>();

    return services;
  }
}

internal sealed class PersistenceWcsCarrierTransferTaskMaterializer(
    PlatformCoreDbContext dbContext,
    CompiledWarehouseTopology topology,
    IWcsOperationalStateStore operationalStateStore) : IWcsCarrierTransferTaskMaterializer
{
  private const string InternalCommandMessageKind = "INTERNAL_COMMAND";
  private const string PlatformEventMessageKind = "PLATFORM_EVENT";
  private const string ExecutionTaskAggregateType = "ExecutionTask";
  private const string WcsProducer = "WCS";
  private const string ActiveReservationState = "ACTIVE";
  private const string ReleasedReservationState = "RELEASED";
  private const string PrepareTransferPhase = "PrepareTransfer";
  private const string MoveCarrierPhase = "MoveCarrier";
  private const string ExitCarrierPhase = "ExitCarrier";
  private const string CompletedPhase = "Completed";
  private const string AwaitingSourceCarrierPhase = "AwaitingSourceCarrier";
  private const string ShuttleTransferCapability = "transfer.lift.hybridPassenger";
  private const string ShuttleCarrierPassengerCapability = "mode.carrierPassenger";
  private const string LiftMoveCapability = "motion.vertical.singleSlot";
  private const string LiftReceiveCapability = "transfer.lift.receiveShuttle";
  private const string LiftDispatchCapability = "transfer.lift.dispatchShuttle";
  private const string LiftOccupancyCapability = "occupancy.singleShuttle";
  private const string TransferRoleSource = "SOURCE";
  private const string TransferRoleReceiver = "RECEIVER";
  private static readonly JsonSerializerOptions ContractJsonSerializerOptions = new(JsonSerializerDefaults.Web)
  {
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  public async ValueTask<CarrierTransferMaterializationResult> MaterializeAsync(
      ExecutionTaskId executionTaskId,
      CancellationToken cancellationToken = default)
  {
    await operationalStateStore.EnsureInitializedAsync(cancellationToken);
    dbContext.ChangeTracker.Clear();

    var context = await LoadContextAsync(executionTaskId, cancellationToken);
    var runtime = context.Runtime;
    var shuttle = context.Shuttle;
    var lift = context.Lift;

    if (runtime.State == ExecutionTaskState.Completed)
    {
      return new CarrierTransferMaterializationResult(
          CarrierTransferMaterializationStatus.Completed,
          CompletedPhase);
    }

    if (runtime.State is ExecutionTaskState.Cancelled or ExecutionTaskState.Failed)
    {
      throw new InvalidOperationException(
          $"Carrier transfer '{executionTaskId}' cannot be materialized from terminal state '{runtime.State}'.");
    }

    if (runtime.State == ExecutionTaskState.Suspended &&
        runtime.ResolutionHint == ExecutionResolutionHint.OperatorAttention)
    {
      return new CarrierTransferMaterializationResult(
          CarrierTransferMaterializationStatus.Suspended,
          runtime.ActiveRuntimePhase ?? MoveCarrierPhase);
    }

    if (runtime.TransferMode != TransferMode.ShuttleRidesHybridLiftWithPayload)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: MoveCarrierPhase,
          reasonCode: "UNSUPPORTED_TRANSFER_MODE",
          resolutionHint: ExecutionResolutionHint.ReplanRequired,
          replanRequired: true,
          cancellationToken);
    }

    if (shuttle.DeviceFamily != DeviceFamily.Shuttle3D)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: MoveCarrierPhase,
          reasonCode: "UNSUPPORTED_CARRIER_TRANSFER_ASSIGNEE",
          resolutionHint: ExecutionResolutionHint.ReplanRequired,
          replanRequired: true,
          cancellationToken);
    }

    if (lift.DeviceFamily != DeviceFamily.HybridLift)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: MoveCarrierPhase,
          reasonCode: "UNSUPPORTED_CARRIER_TRANSFER_PARTICIPANT",
          resolutionHint: ExecutionResolutionHint.ReplanRequired,
          replanRequired: true,
          cancellationToken);
    }

    if (!HasCapabilities(shuttle.ActiveCapabilities, ShuttleTransferCapability, ShuttleCarrierPassengerCapability))
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: PrepareTransferPhase,
          reasonCode: "TRANSFER_CAPABILITY_UNAVAILABLE",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    if (!HasCapabilities(lift.ActiveCapabilities, LiftMoveCapability, LiftReceiveCapability, LiftDispatchCapability, LiftOccupancyCapability))
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: MoveCarrierPhase,
          reasonCode: "CARRIER_CAPABILITY_UNAVAILABLE",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    var snapshot = EvaluateSnapshot(context);
    if (snapshot == CarrierTransferSnapshotState.Conflicting)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: runtime.ActiveRuntimePhase ?? MoveCarrierPhase,
          reasonCode: "CARRIER_TRANSFER_STATE_CONFLICT",
          resolutionHint: ExecutionResolutionHint.OperatorAttention,
          replanRequired: false,
          cancellationToken);
    }

    if (snapshot == CarrierTransferSnapshotState.CarrierNotAtSource)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: AwaitingSourceCarrierPhase,
          reasonCode: "CARRIER_SOURCE_POSITION_REQUIRED",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    if (snapshot == CarrierTransferSnapshotState.Completed)
    {
      return await CompleteAsync(context, cancellationToken);
    }

    await UpsertTransferReservationAsync(context, cancellationToken);

    return snapshot switch
    {
      CarrierTransferSnapshotState.ReadyToBoard => await AuthorizePrepareTransferAsync(context, cancellationToken),
      CarrierTransferSnapshotState.BoardedOnCarrier => await AuthorizeMoveCarrierAsync(context, cancellationToken),
      CarrierTransferSnapshotState.AwaitingBoardConfirmation => await AwaitAsync(
          context,
          PrepareTransferPhase,
          await FindPrepareCommandIdsAsync(context, cancellationToken),
          cancellationToken),
      CarrierTransferSnapshotState.AwaitingMoveConfirmation => await AwaitAsync(
          context,
          MoveCarrierPhase,
          await FindMoveCommandIdsAsync(context, cancellationToken),
          cancellationToken),
      CarrierTransferSnapshotState.AwaitingExitConfirmation => await AwaitAsync(
          context,
          ExitCarrierPhase,
          await FindMoveCommandIdsAsync(context, cancellationToken),
          cancellationToken),
      _ => throw new InvalidOperationException($"Unsupported carrier transfer snapshot state '{snapshot}'.")
    };
  }

  private async ValueTask<CarrierTransferMaterializationResult> AuthorizePrepareTransferAsync(
      CarrierTransferContext context,
      CancellationToken cancellationToken)
  {
    var shuttleSession = await FindActiveSessionAsync(context.Shuttle.DeviceId, cancellationToken);
    var liftSession = await FindActiveSessionAsync(context.Lift.DeviceId, cancellationToken);
    if (shuttleSession is null || liftSession is null)
    {
      return await SuspendAsync(
          context.Runtime,
          runtimePhase: PrepareTransferPhase,
          reasonCode: "PARTICIPANT_SESSION_REQUIRED",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    var issuedOutboxIds = new List<string>();
    var now = DateTimeOffset.UtcNow;
    context.Runtime.State = ExecutionTaskState.InProgress;
    context.Runtime.ActiveRuntimePhase = PrepareTransferPhase;
    context.Runtime.ReasonCode = null;
    context.Runtime.ResolutionHint = null;
    context.Runtime.ReplanRequired = null;

    context.Shuttle.ExecutionState = DeviceExecutionState.Executing;
    context.Shuttle.DispatchStatus = DispatchStatus.Occupied;
    context.Shuttle.LastObservedAt = now;
    context.Lift.ExecutionState = DeviceExecutionState.Executing;
    context.Lift.LastObservedAt = now;

    var shuttleOutboxId = CreateStableIdentifier(
        "wcs.carrierTransfer.prepare",
        context.Runtime.ExecutionTaskId,
        context.Runtime.TaskRevision.ToString(CultureInfo.InvariantCulture),
        context.Shuttle.DeviceId,
        shuttleSession.DeviceSessionId,
        context.SourceStop.TransferPointId.Value,
        TransferRoleSource);
    if (await EnsureSouthboundCommandAsync(
            shuttleOutboxId,
            context.Runtime,
            context.Shuttle,
            shuttleSession,
            messageType: "PrepareTransfer",
            payload: new
            {
              transferPointId = context.SourceStop.TransferPointId.Value,
              role = TransferRoleSource
            },
            now,
            cancellationToken))
    {
      issuedOutboxIds.Add(shuttleOutboxId);
    }

    var liftOutboxId = CreateStableIdentifier(
        "wcs.carrierTransfer.prepare",
        context.Runtime.ExecutionTaskId,
        context.Runtime.TaskRevision.ToString(CultureInfo.InvariantCulture),
        context.Lift.DeviceId,
        liftSession.DeviceSessionId,
        context.SourceStop.TransferPointId.Value,
        TransferRoleReceiver);
    if (await EnsureSouthboundCommandAsync(
            liftOutboxId,
            context.Runtime,
            context.Lift,
            liftSession,
            messageType: "PrepareTransfer",
            payload: new
            {
              transferPointId = context.SourceStop.TransferPointId.Value,
              role = TransferRoleReceiver
            },
            now,
            cancellationToken))
    {
      issuedOutboxIds.Add(liftOutboxId);
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    return new CarrierTransferMaterializationResult(
        issuedOutboxIds.Count == 0
            ? CarrierTransferMaterializationStatus.AwaitingConfirmation
            : CarrierTransferMaterializationStatus.CommandsIssued,
        PrepareTransferPhase,
        issuedOutboxIds.Count == 0 ? [shuttleOutboxId, liftOutboxId] : issuedOutboxIds);
  }

  private async ValueTask<CarrierTransferMaterializationResult> AuthorizeMoveCarrierAsync(
      CarrierTransferContext context,
      CancellationToken cancellationToken)
  {
    var liftSession = await FindActiveSessionAsync(context.Lift.DeviceId, cancellationToken);
    if (liftSession is null)
    {
      return await SuspendAsync(
          context.Runtime,
          runtimePhase: MoveCarrierPhase,
          reasonCode: "PARTICIPANT_SESSION_REQUIRED",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    var now = DateTimeOffset.UtcNow;
    context.Runtime.State = ExecutionTaskState.InProgress;
    context.Runtime.ActiveRuntimePhase = MoveCarrierPhase;
    context.Runtime.ReasonCode = null;
    context.Runtime.ResolutionHint = null;
    context.Runtime.ReplanRequired = null;

    context.Shuttle.ExecutionState = DeviceExecutionState.Executing;
    context.Shuttle.DispatchStatus = DispatchStatus.Occupied;
    context.Shuttle.LastObservedAt = now;
    context.Lift.ExecutionState = DeviceExecutionState.Executing;
    context.Lift.LastObservedAt = now;

    var moveOutboxId = CreateStableIdentifier(
        "wcs.carrierTransfer.move",
        context.Runtime.ExecutionTaskId,
        context.Runtime.TaskRevision.ToString(CultureInfo.InvariantCulture),
        context.Lift.DeviceId,
        liftSession.DeviceSessionId,
        context.TargetStop.CarrierNodeId.Value);
    var wasCreated = await EnsureSouthboundCommandAsync(
        moveOutboxId,
        context.Runtime,
        context.Lift,
        liftSession,
        messageType: "MoveCarrier",
        payload: new
        {
          targetCarrierNode = context.TargetStop.CarrierNodeId.Value
        },
        now,
        cancellationToken);

    await dbContext.SaveChangesAsync(cancellationToken);

    return new CarrierTransferMaterializationResult(
        wasCreated
            ? CarrierTransferMaterializationStatus.CommandsIssued
            : CarrierTransferMaterializationStatus.AwaitingConfirmation,
        MoveCarrierPhase,
        [moveOutboxId]);
  }

  private async ValueTask<CarrierTransferMaterializationResult> CompleteAsync(
      CarrierTransferContext context,
      CancellationToken cancellationToken)
  {
    var commitOutboxIds = new List<string>();
    var now = DateTimeOffset.UtcNow;
    var shuttleSession = await FindActiveSessionAsync(context.Shuttle.DeviceId, cancellationToken);
    var liftSession = await FindActiveSessionAsync(context.Lift.DeviceId, cancellationToken);

    if (shuttleSession is not null)
    {
      var shuttleCommitOutboxId = CreateStableIdentifier(
          "wcs.carrierTransfer.commit",
          context.Runtime.ExecutionTaskId,
          context.Runtime.TaskRevision.ToString(CultureInfo.InvariantCulture),
          context.Shuttle.DeviceId,
          shuttleSession.DeviceSessionId,
          context.TargetStop.TransferPointId.Value);
      if (await EnsureSouthboundCommandAsync(
              shuttleCommitOutboxId,
              context.Runtime,
              context.Shuttle,
              shuttleSession,
              messageType: "CommitTransfer",
              payload: new
              {
                transferPointId = context.TargetStop.TransferPointId.Value
              },
              now,
              cancellationToken))
      {
        commitOutboxIds.Add(shuttleCommitOutboxId);
      }
    }

    if (liftSession is not null)
    {
      var liftCommitOutboxId = CreateStableIdentifier(
          "wcs.carrierTransfer.commit",
          context.Runtime.ExecutionTaskId,
          context.Runtime.TaskRevision.ToString(CultureInfo.InvariantCulture),
          context.Lift.DeviceId,
          liftSession.DeviceSessionId,
          context.TargetStop.TransferPointId.Value);
      if (await EnsureSouthboundCommandAsync(
              liftCommitOutboxId,
              context.Runtime,
              context.Lift,
              liftSession,
              messageType: "CommitTransfer",
              payload: new
              {
                transferPointId = context.TargetStop.TransferPointId.Value
              },
              now,
              cancellationToken))
      {
        commitOutboxIds.Add(liftCommitOutboxId);
      }
    }

    context.Runtime.State = ExecutionTaskState.Completed;
    context.Runtime.ActiveRuntimePhase = CompletedPhase;
    context.Runtime.ReasonCode = null;
    context.Runtime.ResolutionHint = null;
    context.Runtime.ReplanRequired = null;

    context.Shuttle.ExecutionState = DeviceExecutionState.Idle;
    context.Shuttle.DispatchStatus = DispatchStatus.Occupied;
    context.Shuttle.MovementMode = ShuttleMovementMode.Autonomous.ToString();
    context.Shuttle.CarrierId = null;
    context.Shuttle.CurrentNodeId = context.TargetStop.TransferPointId.Value;
    context.Shuttle.LastObservedAt = now;

    context.Lift.ExecutionState = DeviceExecutionState.Idle;
    context.Lift.OccupiedShuttleId = null;
    context.Lift.LastObservedAt = now;

    var reservation = await FindActiveReservationAsync(context.Runtime.ExecutionTaskId, cancellationToken);
    if (reservation is not null)
    {
      reservation.State = ReleasedReservationState;
    }

    var transferCommittedEvent = CreateTransferCommittedEvent(context, now);
    AppendPlatformEvent(transferCommittedEvent);

    await dbContext.SaveChangesAsync(cancellationToken);

    return new CarrierTransferMaterializationResult(
        CarrierTransferMaterializationStatus.Completed,
        CompletedPhase,
        [.. commitOutboxIds, transferCommittedEvent.EventId.Value]);
  }

  private async ValueTask<CarrierTransferMaterializationResult> AwaitAsync(
      CarrierTransferContext context,
      string runtimePhase,
      IReadOnlyList<string> outboxIds,
      CancellationToken cancellationToken)
  {
    context.Runtime.State = ExecutionTaskState.InProgress;
    context.Runtime.ActiveRuntimePhase = runtimePhase;
    context.Runtime.ReasonCode = null;
    context.Runtime.ResolutionHint = null;
    context.Runtime.ReplanRequired = null;

    context.Shuttle.ExecutionState = DeviceExecutionState.Executing;
    context.Shuttle.DispatchStatus = DispatchStatus.Occupied;
    context.Lift.ExecutionState = DeviceExecutionState.Executing;

    await dbContext.SaveChangesAsync(cancellationToken);

    return new CarrierTransferMaterializationResult(
        CarrierTransferMaterializationStatus.AwaitingConfirmation,
        runtimePhase,
        outboxIds);
  }

  private async Task<bool> EnsureSouthboundCommandAsync(
      string outboxId,
      ExecutionTaskRuntimeRecord runtime,
      DeviceShadowRecord device,
      DeviceSessionRecord session,
      string messageType,
      object payload,
      DateTimeOffset createdAt,
      CancellationToken cancellationToken)
  {
    var exists = await dbContext.OutboxMessages
        .AsNoTracking()
        .AnyAsync(record => record.OutboxId == outboxId, cancellationToken);
    if (exists)
    {
      return false;
    }

    dbContext.OutboxMessages.Add(CreateSouthboundCommandOutboxRecord(
        outboxId,
        runtime,
        device,
        session,
        messageType,
        payload,
        createdAt));
    return true;
  }

  private async Task<DeviceSessionRecord?> FindActiveSessionAsync(string deviceId, CancellationToken cancellationToken)
  {
    var now = DateTimeOffset.UtcNow;
    return await dbContext.DeviceSessions
        .SingleOrDefaultAsync(
            record => record.DeviceId == deviceId && record.LeaseUntil > now,
            cancellationToken);
  }

  private async Task UpsertTransferReservationAsync(
      CarrierTransferContext context,
      CancellationToken cancellationToken)
  {
    var reservationId = CreateStableIdentifier("wcs.reservation.carrierTransfer", context.Runtime.ExecutionTaskId);
    var reservedNodeIds = new[]
    {
        context.SourceStop.TransferPointId.Value,
        context.SourceStop.CarrierNodeId.Value,
        context.TargetStop.CarrierNodeId.Value,
        context.TargetStop.TransferPointId.Value
    };
    var reservation = await dbContext.Reservations
        .SingleOrDefaultAsync(record => record.ReservationId == reservationId, cancellationToken);

    if (reservation is null)
    {
      dbContext.Reservations.Add(new ReservationRecord
      {
        ReservationId = reservationId,
        OwnerType = nameof(ReservationOwnerType.ExecutionTask),
        OwnerId = context.Runtime.ExecutionTaskId,
        ReservedNodeIds = reservedNodeIds,
        Horizon = ReservationHorizon.Execution,
        State = ActiveReservationState
      });
      return;
    }

    reservation.OwnerType = nameof(ReservationOwnerType.ExecutionTask);
    reservation.OwnerId = context.Runtime.ExecutionTaskId;
    reservation.ReservedNodeIds = reservedNodeIds;
    reservation.Horizon = ReservationHorizon.Execution;
    reservation.State = ActiveReservationState;
  }

  private async Task<ReservationRecord?> FindActiveReservationAsync(
      string executionTaskId,
      CancellationToken cancellationToken) =>
      await dbContext.Reservations
          .SingleOrDefaultAsync(
              record =>
                  record.OwnerType == nameof(ReservationOwnerType.ExecutionTask) &&
                  record.OwnerId == executionTaskId &&
                  record.Horizon == ReservationHorizon.Execution &&
                  record.State == ActiveReservationState,
              cancellationToken);

  private async Task<IReadOnlyList<string>> FindPrepareCommandIdsAsync(
      CarrierTransferContext context,
      CancellationToken cancellationToken)
  {
    var records = await LoadTaskCommandOutboxAsync(context.Runtime.ExecutionTaskId, cancellationToken);
    return records
        .Where(record => HasMessageType(record.Payload, "PrepareTransfer"))
        .Select(static record => record.OutboxId)
        .ToArray();
  }

  private async Task<IReadOnlyList<string>> FindMoveCommandIdsAsync(
      CarrierTransferContext context,
      CancellationToken cancellationToken)
  {
    var records = await LoadTaskCommandOutboxAsync(context.Runtime.ExecutionTaskId, cancellationToken);
    return records
        .Where(record => HasMessageType(record.Payload, "MoveCarrier"))
        .Select(static record => record.OutboxId)
        .ToArray();
  }

  private async Task<IReadOnlyList<OutboxMessageRecord>> LoadTaskCommandOutboxAsync(
      string executionTaskId,
      CancellationToken cancellationToken) =>
      await dbContext.OutboxMessages
          .AsNoTracking()
          .Where(record =>
              record.AggregateType == ExecutionTaskAggregateType &&
              record.AggregateId == executionTaskId &&
              record.MessageKind == InternalCommandMessageKind)
          .OrderBy(record => record.CreatedAt)
          .ThenBy(record => record.OutboxId)
          .ToListAsync(cancellationToken);

  private async Task<CarrierTransferContext> LoadContextAsync(
      ExecutionTaskId executionTaskId,
      CancellationToken cancellationToken)
  {
    var runtime = await dbContext.ExecutionTaskRuntime
        .SingleOrDefaultAsync(record => record.ExecutionTaskId == executionTaskId.Value, cancellationToken)
        ?? throw new InvalidOperationException($"Execution task runtime '{executionTaskId}' was not found.");

    if (runtime.TaskType != ExecutionTaskType.CarrierTransfer)
    {
      throw new InvalidOperationException($"Execution task '{executionTaskId}' is not a CarrierTransfer task.");
    }

    if (!string.Equals(runtime.AssigneeType, nameof(ExecutionActorType.Device), StringComparison.Ordinal))
    {
      throw new InvalidOperationException($"Carrier transfer '{executionTaskId}' must be assigned to a device.");
    }

    if (string.IsNullOrWhiteSpace(runtime.SourceNodeId) || string.IsNullOrWhiteSpace(runtime.TargetNodeId))
    {
      throw new InvalidOperationException($"Carrier transfer '{executionTaskId}' must define both source and target transfer points.");
    }

    var participantDeviceId = ResolveParticipantDeviceId(runtime.ParticipantRefs);
    var shuttle = await dbContext.DeviceShadows
        .SingleOrDefaultAsync(record => record.DeviceId == runtime.AssigneeId, cancellationToken)
        ?? throw new InvalidOperationException($"Device shadow '{runtime.AssigneeId}' was not found for task '{executionTaskId}'.");
    var lift = await dbContext.DeviceShadows
        .SingleOrDefaultAsync(record => record.DeviceId == participantDeviceId.Value, cancellationToken)
        ?? throw new InvalidOperationException($"Device shadow '{participantDeviceId}' was not found for task '{executionTaskId}'.");

    var sourceTransferPointId = new NodeId(runtime.SourceNodeId);
    var targetTransferPointId = new NodeId(runtime.TargetNodeId);
    if (!topology.TryGetShaftStopByTransferPoint(sourceTransferPointId, out var sourceStop))
    {
      throw new InvalidOperationException(
          $"Carrier transfer '{executionTaskId}' references unknown source transfer point '{sourceTransferPointId}'.");
    }

    if (!topology.TryGetShaftStopByTransferPoint(targetTransferPointId, out var targetStop))
    {
      throw new InvalidOperationException(
          $"Carrier transfer '{executionTaskId}' references unknown target transfer point '{targetTransferPointId}'.");
    }

    if (sourceStop.ShaftId != targetStop.ShaftId ||
        sourceStop.CarrierDeviceId != participantDeviceId ||
        targetStop.CarrierDeviceId != participantDeviceId)
    {
      throw new InvalidOperationException(
          $"Carrier transfer '{executionTaskId}' does not match the compiled shaft topology for participant '{participantDeviceId}'.");
    }

    return new CarrierTransferContext(runtime, shuttle, lift, sourceStop, targetStop);
  }

  private static DeviceId ResolveParticipantDeviceId(string participantRefs)
  {
    using var document = JsonDocument.Parse(participantRefs);
    string? deviceId = null;

    foreach (var participant in document.RootElement.EnumerateArray())
    {
      if (!string.Equals(participant.GetProperty("type").GetString(), "device", StringComparison.Ordinal))
      {
        continue;
      }

      if (deviceId is not null)
      {
        throw new InvalidOperationException("Carrier transfer requires exactly one device participant.");
      }

      deviceId = participant.GetProperty("resourceId").GetString();
    }

    return string.IsNullOrWhiteSpace(deviceId)
        ? throw new InvalidOperationException("Carrier transfer requires exactly one device participant.")
        : new DeviceId(deviceId);
  }

  private static bool HasCapabilities(IEnumerable<string> activeCapabilities, params string[] requiredCapabilities)
  {
    var knownCapabilities = activeCapabilities.ToHashSet(StringComparer.Ordinal);
    return requiredCapabilities.All(knownCapabilities.Contains);
  }

  private static CarrierTransferSnapshotState EvaluateSnapshot(CarrierTransferContext context)
  {
    var shuttle = context.Shuttle;
    var lift = context.Lift;

    var shuttleAutonomous = string.Equals(shuttle.MovementMode, nameof(ShuttleMovementMode.Autonomous), StringComparison.Ordinal);
    var shuttlePassenger = string.Equals(shuttle.MovementMode, nameof(ShuttleMovementMode.CarrierPassenger), StringComparison.Ordinal);
    var shuttleCarrierId = string.IsNullOrWhiteSpace(shuttle.CarrierId) ? null : shuttle.CarrierId;
    var liftOccupiedShuttleId = string.IsNullOrWhiteSpace(lift.OccupiedShuttleId) ? null : lift.OccupiedShuttleId;
    var shuttleNodeId = string.IsNullOrWhiteSpace(shuttle.CurrentNodeId) ? null : shuttle.CurrentNodeId;
    var liftNodeId = string.IsNullOrWhiteSpace(lift.CurrentNodeId) ? null : lift.CurrentNodeId;
    var sourceTransferPointId = context.SourceStop.TransferPointId.Value;
    var targetTransferPointId = context.TargetStop.TransferPointId.Value;
    var sourceCarrierNodeId = context.SourceStop.CarrierNodeId.Value;
    var targetCarrierNodeId = context.TargetStop.CarrierNodeId.Value;

    if (shuttlePassenger)
    {
      if (!string.Equals(shuttleCarrierId, lift.DeviceId, StringComparison.Ordinal) ||
          !string.Equals(liftOccupiedShuttleId, shuttle.DeviceId, StringComparison.Ordinal) ||
          !string.Equals(shuttleNodeId, liftNodeId, StringComparison.Ordinal))
      {
        return CarrierTransferSnapshotState.Conflicting;
      }

      return string.Equals(liftNodeId, targetCarrierNodeId, StringComparison.Ordinal)
          ? CarrierTransferSnapshotState.AwaitingExitConfirmation
          : CarrierTransferSnapshotState.BoardedOnCarrier;
    }

    if (!shuttleAutonomous || shuttleCarrierId is not null)
    {
      return CarrierTransferSnapshotState.Conflicting;
    }

    if (liftOccupiedShuttleId is not null)
    {
      return CarrierTransferSnapshotState.Conflicting;
    }

    if (string.Equals(shuttleNodeId, targetTransferPointId, StringComparison.Ordinal))
    {
      return CarrierTransferSnapshotState.Completed;
    }

    if (!string.Equals(shuttleNodeId, sourceTransferPointId, StringComparison.Ordinal))
    {
      return CarrierTransferSnapshotState.Conflicting;
    }

    if (!string.Equals(liftNodeId, sourceCarrierNodeId, StringComparison.Ordinal))
    {
      return CarrierTransferSnapshotState.CarrierNotAtSource;
    }

    return context.Runtime.State == ExecutionTaskState.InProgress &&
           string.Equals(context.Runtime.ActiveRuntimePhase, PrepareTransferPhase, StringComparison.Ordinal)
        ? CarrierTransferSnapshotState.AwaitingBoardConfirmation
        : CarrierTransferSnapshotState.ReadyToBoard;
  }

  private void AppendPlatformEvent<TPayload>(CanonicalPlatformEvent<TPayload> platformEvent)
  {
    dbContext.OutboxMessages.Add(new OutboxMessageRecord
    {
      OutboxId = platformEvent.EventId.Value,
      Producer = WcsProducer,
      MessageKind = PlatformEventMessageKind,
      AggregateType = ExecutionTaskAggregateType,
      AggregateId = platformEvent.Envelope.CausationId?.Value ?? platformEvent.Envelope.CorrelationId.Value,
      CorrelationId = platformEvent.Envelope.CorrelationId.Value,
      CausationId = platformEvent.Envelope.CausationId?.Value,
      Payload = SerializeOutboxPayload(platformEvent),
      CreatedAt = platformEvent.OccurredAt
    });
    dbContext.PlatformEventJournal.Add(new PlatformEventJournalRecord
    {
      EventId = platformEvent.EventId.Value,
      EventName = platformEvent.EventName,
      EventVersion = platformEvent.EventVersion,
      OccurredAt = platformEvent.OccurredAt,
      CorrelationId = platformEvent.Envelope.CorrelationId.Value,
      CausationId = platformEvent.Envelope.CausationId?.Value,
      Visibility = platformEvent.Visibility,
      Payload = JsonSerializer.Serialize(platformEvent.Payload, ContractJsonSerializerOptions)
    });
  }

  private static CanonicalPlatformEvent<TransferCommittedPayload> CreateTransferCommittedEvent(
      CarrierTransferContext context,
      DateTimeOffset occurredAt)
  {
    var eventId = CreateStableIdentifier(
        "evt.transferCommitted",
        context.Runtime.ExecutionTaskId,
        context.TargetStop.TransferPointId.Value,
        context.Runtime.TransferMode?.ToString() ?? string.Empty);

    return new CanonicalPlatformEvent<TransferCommittedPayload>(
        TransferEventNames.TransferCommitted,
        new ContractEnvelope(
            new EnvelopeId(eventId),
            new CorrelationId(context.Runtime.CorrelationId),
            new CausationId(context.Runtime.ExecutionTaskId)),
        occurredAt,
        PlatformEventVisibility.Operations,
        new TransferCommittedPayload(
            context.Runtime.ExecutionTaskId,
            context.Runtime.TransferMode?.ToString() ?? throw new InvalidOperationException("Transfer mode is required."),
            context.TargetStop.TransferPointId.Value,
            [
                new TransferParticipantPayload(nameof(ExecutionActorType.Device), context.Shuttle.DeviceId),
                new TransferParticipantPayload(nameof(ExecutionActorType.Device), context.Lift.DeviceId)
            ]));
  }

  private static string SerializeOutboxPayload<TPayload>(CanonicalPlatformEvent<TPayload> platformEvent) =>
      JsonSerializer.Serialize(
          new StoredPlatformEventEnvelope<TPayload>(
              platformEvent.EventId.Value,
              platformEvent.EventName,
              platformEvent.EventVersion.Value,
              platformEvent.OccurredAt,
              platformEvent.Envelope.CorrelationId.Value,
              platformEvent.Envelope.CausationId?.Value,
              platformEvent.Visibility.ToString(),
              platformEvent.Payload),
          ContractJsonSerializerOptions);

  private static OutboxMessageRecord CreateSouthboundCommandOutboxRecord(
      string outboxId,
      ExecutionTaskRuntimeRecord runtime,
      DeviceShadowRecord device,
      DeviceSessionRecord session,
      string messageType,
      object payload,
      DateTimeOffset createdAt)
  {
    var serializedPayload = JsonSerializer.Serialize(
        new
        {
          messageId = outboxId,
          schemaVersion = "v0",
          messageType,
          correlationId = runtime.CorrelationId,
          causationId = runtime.ExecutionTaskId,
          deviceId = device.DeviceId,
          family = device.DeviceFamily.ToString(),
          sessionId = session.DeviceSessionId,
          platformTime = createdAt,
          payload
        });

    return new OutboxMessageRecord
    {
      OutboxId = outboxId,
      Producer = WcsProducer,
      MessageKind = InternalCommandMessageKind,
      AggregateType = ExecutionTaskAggregateType,
      AggregateId = runtime.ExecutionTaskId,
      CorrelationId = runtime.CorrelationId,
      CausationId = runtime.ExecutionTaskId,
      Payload = serializedPayload,
      CreatedAt = createdAt
    };
  }

  private async ValueTask<CarrierTransferMaterializationResult> SuspendAsync(
      ExecutionTaskRuntimeRecord runtime,
      string runtimePhase,
      string reasonCode,
      ExecutionResolutionHint resolutionHint,
      bool replanRequired,
      CancellationToken cancellationToken)
  {
    runtime.State = ExecutionTaskState.Suspended;
    runtime.ActiveRuntimePhase = runtimePhase;
    runtime.ReasonCode = reasonCode;
    runtime.ResolutionHint = resolutionHint;
    runtime.ReplanRequired = replanRequired;

    await dbContext.SaveChangesAsync(cancellationToken);

    return new CarrierTransferMaterializationResult(
        CarrierTransferMaterializationStatus.Suspended,
        runtimePhase);
  }

  private static bool HasMessageType(string payload, string messageType)
  {
    using var document = JsonDocument.Parse(payload);
    return string.Equals(document.RootElement.GetProperty("messageType").GetString(), messageType, StringComparison.Ordinal);
  }

  private static string CreateStableIdentifier(string prefix, params string[] parts)
  {
    var payload = string.Join("|", parts);
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    return $"{prefix}.{hash[..24]}";
  }

  private readonly record struct CarrierTransferContext(
      ExecutionTaskRuntimeRecord Runtime,
      DeviceShadowRecord Shuttle,
      DeviceShadowRecord Lift,
      CompiledCarrierShaftStop SourceStop,
      CompiledCarrierShaftStop TargetStop);

  private enum CarrierTransferSnapshotState
  {
    ReadyToBoard,
    AwaitingBoardConfirmation,
    BoardedOnCarrier,
    AwaitingMoveConfirmation,
    AwaitingExitConfirmation,
    Completed,
    CarrierNotAtSource,
    Conflicting
  }

  private sealed record StoredPlatformEventEnvelope<TPayload>(
      string EventId,
      string EventName,
      string EventVersion,
      DateTimeOffset OccurredAt,
      string CorrelationId,
      string? CausationId,
      string Visibility,
      TPayload Payload);
}
