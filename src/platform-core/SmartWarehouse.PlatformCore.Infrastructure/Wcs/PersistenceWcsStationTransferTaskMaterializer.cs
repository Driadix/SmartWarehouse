using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Contracts;
using SmartWarehouse.PlatformCore.Application.Wcs;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

namespace SmartWarehouse.PlatformCore.Infrastructure.Wcs;

public static class PersistenceWcsStationTransferTaskMaterializationServiceCollectionExtensions
{
  public static IServiceCollection AddPersistenceWcsStationTransferTaskMaterialization(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddScoped<IWcsStationTransferTaskMaterializer, PersistenceWcsStationTransferTaskMaterializer>();

    return services;
  }
}

internal sealed class PersistenceWcsStationTransferTaskMaterializer(
    PlatformCoreDbContext dbContext,
    IWcsOperationalStateStore operationalStateStore) : IWcsStationTransferTaskMaterializer
{
  private const string PassiveStationTransferCapability = "transfer.station.passive";
  private const string ActiveReservationState = "ACTIVE";
  private const string ReleasedReservationState = "RELEASED";
  private const string InternalCommandMessageKind = "INTERNAL_COMMAND";
  private const string PlatformEventMessageKind = "PLATFORM_EVENT";
  private const string ExecutionTaskAggregateType = "ExecutionTask";
  private const string WcsProducer = "WCS";
  private const string ReachingBoundaryPhase = "ReachingBoundary";
  private const string BoundaryPositionConfirmedPhase = "BoundaryPositionConfirmed";
  private const string CommitCustodyPhase = "CommitCustody";
  private const string AwaitingBoundaryPositionPhase = "AwaitingBoundaryPosition";
  private const string AwaitingStationReadinessPhase = "AwaitingStationReadiness";
  private const string CompletedPhase = "Completed";
  private static readonly JsonSerializerOptions ContractJsonSerializerOptions = new(JsonSerializerDefaults.Web)
  {
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  public async ValueTask<StationTransferMaterializationResult> MaterializeAsync(
      ExecutionTaskId executionTaskId,
      CancellationToken cancellationToken = default)
  {
    await operationalStateStore.EnsureInitializedAsync(cancellationToken);
    dbContext.ChangeTracker.Clear();

    var context = await LoadContextAsync(executionTaskId, cancellationToken);
    var runtime = context.Runtime;
    var shuttle = context.Shuttle;
    var station = context.Station;
    var attachedNodeId = new NodeId(station.AttachedNodeId);

    if (runtime.State == ExecutionTaskState.Completed)
    {
      return new StationTransferMaterializationResult(StationTransferMaterializationStatus.Completed);
    }

    if (runtime.State is ExecutionTaskState.Cancelled or ExecutionTaskState.Failed)
    {
      throw new InvalidOperationException(
          $"Station transfer '{executionTaskId}' cannot be materialized from terminal state '{runtime.State}'.");
    }

    if (runtime.State == ExecutionTaskState.Suspended &&
        runtime.ResolutionHint == ExecutionResolutionHint.OperatorAttention)
    {
      return new StationTransferMaterializationResult(StationTransferMaterializationStatus.Suspended);
    }

    if (runtime.State == ExecutionTaskState.InProgress &&
        string.Equals(runtime.ActiveRuntimePhase, BoundaryPositionConfirmedPhase, StringComparison.Ordinal))
    {
      return new StationTransferMaterializationResult(StationTransferMaterializationStatus.BoundaryPositionConfirmed);
    }

    if (shuttle.DeviceFamily != DeviceFamily.Shuttle3D)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: CommitCustodyPhase,
          reasonCode: "UNSUPPORTED_STATION_TRANSFER_ASSIGNEE",
          resolutionHint: ExecutionResolutionHint.ReplanRequired,
          replanRequired: true,
          cancellationToken);
    }

    if (!shuttle.ActiveCapabilities.Contains(PassiveStationTransferCapability, StringComparer.Ordinal))
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: CommitCustodyPhase,
          reasonCode: "STATION_TRANSFER_CAPABILITY_UNAVAILABLE",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    NodeId? currentNodeId = string.IsNullOrWhiteSpace(shuttle.CurrentNodeId) ? null : new NodeId(shuttle.CurrentNodeId);
    if (currentNodeId == attachedNodeId)
    {
      return await ConfirmBoundaryPositionAsync(runtime, shuttle, station, cancellationToken);
    }

    if (runtime.State == ExecutionTaskState.InProgress &&
        string.Equals(runtime.ActiveRuntimePhase, ReachingBoundaryPhase, StringComparison.Ordinal))
    {
      var activeReservation = await FindActiveReservationAsync(executionTaskId, cancellationToken);
      var commandOutboxId = await FindActiveBoundaryCommandOutboxIdAsync(runtime, cancellationToken);
      if (activeReservation is not null && commandOutboxId is not null)
      {
        return new StationTransferMaterializationResult(
            StationTransferMaterializationStatus.AlreadyAuthorized,
            activeReservation.ReservedNodeIds.Select(static nodeId => new NodeId(nodeId)),
            commandOutboxId);
      }
    }

    var blockingReservationExists = await dbContext.Reservations
        .AsNoTracking()
        .AnyAsync(
            record =>
                record.Horizon == ReservationHorizon.Execution &&
                record.State == ActiveReservationState &&
                !(record.OwnerType == nameof(ReservationOwnerType.ExecutionTask) && record.OwnerId == executionTaskId.Value) &&
                record.ReservedNodeIds.Contains(attachedNodeId.Value),
            cancellationToken);
    if (blockingReservationExists)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: AwaitingBoundaryPositionPhase,
          reasonCode: "BOUNDARY_NODE_BLOCKED",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    var now = DateTimeOffset.UtcNow;
    var deviceSession = await dbContext.DeviceSessions
        .SingleOrDefaultAsync(
            record => record.DeviceId == shuttle.DeviceId && record.LeaseUntil > now,
            cancellationToken);
    if (deviceSession is null)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: AwaitingBoundaryPositionPhase,
          reasonCode: "DEVICE_SESSION_REQUIRED",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    await UpsertBoundaryReservationAsync(executionTaskId, attachedNodeId, cancellationToken);

    runtime.State = ExecutionTaskState.InProgress;
    runtime.ActiveRuntimePhase = ReachingBoundaryPhase;
    runtime.ReasonCode = null;
    runtime.ResolutionHint = null;
    runtime.ReplanRequired = null;

    shuttle.ExecutionState = DeviceExecutionState.Executing;
    shuttle.DispatchStatus = DispatchStatus.Occupied;
    shuttle.LastObservedAt = now;

    var newCommandOutboxId = CreateStableIdentifier(
        "wcs.stationTransfer",
        executionTaskId.Value,
        runtime.TaskRevision.ToString(CultureInfo.InvariantCulture),
        deviceSession.DeviceSessionId);
    var existingOutboxRecord = await dbContext.OutboxMessages
        .SingleOrDefaultAsync(record => record.OutboxId == newCommandOutboxId, cancellationToken);
    if (existingOutboxRecord is null)
    {
      dbContext.OutboxMessages.Add(CreateGrantMotionWindowOutboxRecord(
          newCommandOutboxId,
          runtime,
          shuttle,
          deviceSession,
          attachedNodeId,
          now));
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    return new StationTransferMaterializationResult(
        StationTransferMaterializationStatus.BoundaryMotionAuthorized,
        [attachedNodeId],
        newCommandOutboxId);
  }

  public async ValueTask<StationTransferMaterializationResult> ConfirmTransferAsync(
      ExecutionTaskId executionTaskId,
      PayloadId payloadId,
      CancellationToken cancellationToken = default)
  {
    await operationalStateStore.EnsureInitializedAsync(cancellationToken);
    dbContext.ChangeTracker.Clear();

    var context = await LoadContextAsync(executionTaskId, cancellationToken);
    var runtime = context.Runtime;
    var shuttle = context.Shuttle;
    var station = context.Station;
    var attachedNodeId = new NodeId(station.AttachedNodeId);
    NodeId? currentNodeId = string.IsNullOrWhiteSpace(shuttle.CurrentNodeId) ? null : new NodeId(shuttle.CurrentNodeId);

    if (runtime.State == ExecutionTaskState.Completed)
    {
      return ValidateCompletedResult(runtime, shuttle, station, payloadId);
    }

    if (currentNodeId != attachedNodeId)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: CommitCustodyPhase,
          reasonCode: "TRANSFER_FACT_POSITION_CONFLICT",
          resolutionHint: ExecutionResolutionHint.OperatorAttention,
          replanRequired: false,
          cancellationToken);
    }

    if (station.Readiness != StationReadiness.Ready)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: CommitCustodyPhase,
          reasonCode: "TRANSFER_FACT_READINESS_CONFLICT",
          resolutionHint: ExecutionResolutionHint.OperatorAttention,
          replanRequired: false,
          cancellationToken);
    }

    var transition = TryBuildCustodyTransition(runtime, shuttle, station, payloadId);
    if (transition is null)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: CommitCustodyPhase,
          reasonCode: "TRANSFER_FACT_CUSTODY_CONFLICT",
          resolutionHint: ExecutionResolutionHint.OperatorAttention,
          replanRequired: false,
          cancellationToken);
    }

    runtime.State = ExecutionTaskState.Completed;
    runtime.ActiveRuntimePhase = CompletedPhase;
    runtime.ReasonCode = null;
    runtime.ResolutionHint = null;
    runtime.ReplanRequired = null;

    var now = DateTimeOffset.UtcNow;
    shuttle.ExecutionState = DeviceExecutionState.Idle;
    shuttle.DispatchStatus = transition.Value.NewDispatchStatus;
    shuttle.CarriedPayloadId = transition.Value.DevicePayloadId;
    shuttle.LastObservedAt = now;

    station.CurrentPayloadId = transition.Value.StationPayloadId;
    station.LastUpdatedAt = now;

    var reservation = await FindActiveReservationAsync(executionTaskId, cancellationToken);
    if (reservation is not null)
    {
      reservation.State = ReleasedReservationState;
    }

    AppendPlatformEvent(CreatePayloadCustodyChangedEvent(runtime, transition.Value, now));

    await dbContext.SaveChangesAsync(cancellationToken);

    return new StationTransferMaterializationResult(
        StationTransferMaterializationStatus.Completed,
        outboxId: transition.Value.EventId);
  }

  private async ValueTask<StationTransferMaterializationResult> ConfirmBoundaryPositionAsync(
      ExecutionTaskRuntimeRecord runtime,
      DeviceShadowRecord shuttle,
      StationBoundaryStateRecord station,
      CancellationToken cancellationToken)
  {
    if (station.Readiness != StationReadiness.Ready)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: AwaitingStationReadinessPhase,
          reasonCode: "STATION_NOT_READY",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    runtime.State = ExecutionTaskState.InProgress;
    runtime.ActiveRuntimePhase = BoundaryPositionConfirmedPhase;
    runtime.ReasonCode = null;
    runtime.ResolutionHint = null;
    runtime.ReplanRequired = null;

    shuttle.ExecutionState = DeviceExecutionState.Executing;
    shuttle.DispatchStatus = DispatchStatus.Occupied;

    await dbContext.SaveChangesAsync(cancellationToken);

    return new StationTransferMaterializationResult(StationTransferMaterializationStatus.BoundaryPositionConfirmed);
  }

  private async Task UpsertBoundaryReservationAsync(
      ExecutionTaskId executionTaskId,
      NodeId attachedNodeId,
      CancellationToken cancellationToken)
  {
    var reservationId = CreateStableIdentifier("wcs.reservation.stationTransfer", executionTaskId.Value);
    var reservation = await dbContext.Reservations
        .SingleOrDefaultAsync(record => record.ReservationId == reservationId, cancellationToken);

    if (reservation is null)
    {
      dbContext.Reservations.Add(new ReservationRecord
      {
        ReservationId = reservationId,
        OwnerType = nameof(ReservationOwnerType.ExecutionTask),
        OwnerId = executionTaskId.Value,
        ReservedNodeIds = [attachedNodeId.Value],
        Horizon = ReservationHorizon.Execution,
        State = ActiveReservationState
      });
      return;
    }

    reservation.OwnerType = nameof(ReservationOwnerType.ExecutionTask);
    reservation.OwnerId = executionTaskId.Value;
    reservation.ReservedNodeIds = [attachedNodeId.Value];
    reservation.Horizon = ReservationHorizon.Execution;
    reservation.State = ActiveReservationState;
  }

  private async Task<ReservationRecord?> FindActiveReservationAsync(
      ExecutionTaskId executionTaskId,
      CancellationToken cancellationToken) =>
      await dbContext.Reservations
          .SingleOrDefaultAsync(
              record =>
                  record.OwnerType == nameof(ReservationOwnerType.ExecutionTask) &&
                  record.OwnerId == executionTaskId.Value &&
                  record.Horizon == ReservationHorizon.Execution &&
                  record.State == ActiveReservationState,
              cancellationToken);

  private async Task<string?> FindActiveBoundaryCommandOutboxIdAsync(
      ExecutionTaskRuntimeRecord runtime,
      CancellationToken cancellationToken)
  {
    var outboxRecord = await dbContext.OutboxMessages
        .AsNoTracking()
        .Where(record =>
            record.AggregateType == ExecutionTaskAggregateType &&
            record.AggregateId == runtime.ExecutionTaskId &&
            record.MessageKind == InternalCommandMessageKind)
        .OrderByDescending(record => record.CreatedAt)
        .ThenByDescending(record => record.OutboxId)
        .FirstOrDefaultAsync(cancellationToken);

    if (outboxRecord is null)
    {
      return null;
    }

    using var document = JsonDocument.Parse(outboxRecord.Payload);
    return string.Equals(
        document.RootElement.GetProperty("messageType").GetString(),
        "GrantMotionWindow",
        StringComparison.Ordinal)
        ? outboxRecord.OutboxId
        : null;
  }

  private async Task<StationTransferContext> LoadContextAsync(
      ExecutionTaskId executionTaskId,
      CancellationToken cancellationToken)
  {
    var runtime = await dbContext.ExecutionTaskRuntime
        .SingleOrDefaultAsync(record => record.ExecutionTaskId == executionTaskId.Value, cancellationToken)
        ?? throw new InvalidOperationException($"Execution task runtime '{executionTaskId}' was not found.");

    if (runtime.TaskType != ExecutionTaskType.StationTransfer)
    {
      throw new InvalidOperationException($"Execution task '{executionTaskId}' is not a StationTransfer task.");
    }

    if (!string.Equals(runtime.AssigneeType, nameof(ExecutionActorType.Device), StringComparison.Ordinal))
    {
      throw new InvalidOperationException($"Station transfer '{executionTaskId}' must be assigned to a device.");
    }

    if (string.IsNullOrWhiteSpace(runtime.TargetNodeId))
    {
      throw new InvalidOperationException($"Station transfer '{executionTaskId}' does not define a target node.");
    }

    var stationId = ResolveStationId(runtime.ParticipantRefs);
    var shuttle = await dbContext.DeviceShadows
        .SingleOrDefaultAsync(record => record.DeviceId == runtime.AssigneeId, cancellationToken)
        ?? throw new InvalidOperationException($"Device shadow '{runtime.AssigneeId}' was not found for task '{executionTaskId}'.");
    var station = await dbContext.StationBoundaryStates
        .SingleOrDefaultAsync(record => record.StationId == stationId.Value, cancellationToken)
        ?? throw new InvalidOperationException($"Station state '{stationId}' was not found for task '{executionTaskId}'.");

    if (!string.Equals(runtime.TargetNodeId, station.AttachedNodeId, StringComparison.Ordinal))
    {
      throw new InvalidOperationException(
          $"Station transfer '{executionTaskId}' targets node '{runtime.TargetNodeId}', but station '{stationId}' is attached to '{station.AttachedNodeId}'.");
    }

    return new StationTransferContext(runtime, shuttle, station);
  }

  private static StationId ResolveStationId(string participantRefs)
  {
    using var document = JsonDocument.Parse(participantRefs);
    string? stationId = null;

    foreach (var participant in document.RootElement.EnumerateArray())
    {
      if (!string.Equals(
              participant.GetProperty("type").GetString(),
              "stationBoundary",
              StringComparison.Ordinal))
      {
        continue;
      }

      if (stationId is not null)
      {
        throw new InvalidOperationException("Station transfer requires exactly one station participant.");
      }

      stationId = participant.GetProperty("resourceId").GetString();
    }

    return string.IsNullOrWhiteSpace(stationId)
        ? throw new InvalidOperationException("Station transfer requires exactly one station participant.")
        : new StationId(stationId);
  }

  private static CustodyTransition? TryBuildCustodyTransition(
      ExecutionTaskRuntimeRecord runtime,
      DeviceShadowRecord shuttle,
      StationBoundaryStateRecord station,
      PayloadId payloadId)
  {
    var payload = payloadId.Value;
    return station.StationType switch
    {
      StationType.Load when string.Equals(station.CurrentPayloadId, payload, StringComparison.Ordinal) &&
                             (string.IsNullOrWhiteSpace(shuttle.CarriedPayloadId) ||
                              string.Equals(shuttle.CarriedPayloadId, payload, StringComparison.Ordinal)) =>
          new CustodyTransition(
              EventId: CreateStableIdentifier(
                  "evt.payloadCustody",
                  runtime.ExecutionTaskId,
                  payload,
                  nameof(PayloadHolderType.StationBoundary),
                  station.StationId,
                  nameof(PayloadHolderType.Device),
                  shuttle.DeviceId),
              PreviousHolderType: PayloadHolderType.StationBoundary,
              PreviousHolderId: station.StationId,
              NewHolderType: PayloadHolderType.Device,
              NewHolderId: shuttle.DeviceId,
              DevicePayloadId: payload,
              StationPayloadId: null,
              NewDispatchStatus: DispatchStatus.Occupied),
      StationType.Unload when string.Equals(shuttle.CarriedPayloadId, payload, StringComparison.Ordinal) &&
                               (string.IsNullOrWhiteSpace(station.CurrentPayloadId) ||
                                string.Equals(station.CurrentPayloadId, payload, StringComparison.Ordinal)) =>
          new CustodyTransition(
              EventId: CreateStableIdentifier(
                  "evt.payloadCustody",
                  runtime.ExecutionTaskId,
                  payload,
                  nameof(PayloadHolderType.Device),
                  shuttle.DeviceId,
                  nameof(PayloadHolderType.StationBoundary),
                  station.StationId),
              PreviousHolderType: PayloadHolderType.Device,
              PreviousHolderId: shuttle.DeviceId,
              NewHolderType: PayloadHolderType.StationBoundary,
              NewHolderId: station.StationId,
              DevicePayloadId: null,
              StationPayloadId: payload,
              NewDispatchStatus: DispatchStatus.Available),
      _ => null
    };
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

  private static CanonicalPlatformEvent<PayloadCustodyChangedPayload> CreatePayloadCustodyChangedEvent(
      ExecutionTaskRuntimeRecord runtime,
      CustodyTransition transition,
      DateTimeOffset occurredAt) =>
      new(
          PayloadCustodyEventNames.PayloadCustodyChanged,
          new ContractEnvelope(
              new EnvelopeId(transition.EventId),
              new CorrelationId(runtime.CorrelationId),
              new CausationId(runtime.ExecutionTaskId)),
          occurredAt,
          PlatformEventVisibility.Operations,
          new PayloadCustodyChangedPayload(
              transition.DevicePayloadId ?? transition.StationPayloadId ?? throw new InvalidOperationException("Payload identifier is required."),
              transition.PreviousHolderType.ToString(),
              transition.PreviousHolderId,
              transition.NewHolderType.ToString(),
              transition.NewHolderId));

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

  private static OutboxMessageRecord CreateGrantMotionWindowOutboxRecord(
      string outboxId,
      ExecutionTaskRuntimeRecord runtime,
      DeviceShadowRecord shuttle,
      DeviceSessionRecord deviceSession,
      NodeId attachedNodeId,
      DateTimeOffset createdAt)
  {
    var payload = JsonSerializer.Serialize(
        new
        {
          messageId = outboxId,
          schemaVersion = "v0",
          messageType = "GrantMotionWindow",
          correlationId = runtime.CorrelationId,
          causationId = runtime.ExecutionTaskId,
          deviceId = shuttle.DeviceId,
          family = shuttle.DeviceFamily.ToString(),
          sessionId = deviceSession.DeviceSessionId,
          platformTime = createdAt,
          payload = new
          {
            nodePath = new[] { attachedNodeId.Value }
          }
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
      Payload = payload,
      CreatedAt = createdAt
    };
  }

  private async ValueTask<StationTransferMaterializationResult> SuspendAsync(
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

    return new StationTransferMaterializationResult(StationTransferMaterializationStatus.Suspended);
  }

  private static string CreateStableIdentifier(string prefix, params string[] parts)
  {
    var payload = string.Join("|", parts);
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    return $"{prefix}.{hash[..24]}";
  }

  private static StationTransferMaterializationResult ValidateCompletedResult(
      ExecutionTaskRuntimeRecord runtime,
      DeviceShadowRecord shuttle,
      StationBoundaryStateRecord station,
      PayloadId payloadId)
  {
    var payload = payloadId.Value;
    var isConsistent = station.StationType switch
    {
      StationType.Load => string.Equals(shuttle.CarriedPayloadId, payload, StringComparison.Ordinal) &&
                          string.IsNullOrWhiteSpace(station.CurrentPayloadId),
      StationType.Unload => string.Equals(station.CurrentPayloadId, payload, StringComparison.Ordinal) &&
                            string.IsNullOrWhiteSpace(shuttle.CarriedPayloadId),
      _ => false
    };

    if (!isConsistent)
    {
      throw new InvalidOperationException(
          $"Completed station transfer for station '{station.StationId}' does not match payload '{payloadId}'.");
    }

    var eventId = station.StationType switch
    {
      StationType.Load => CreateStableIdentifier(
          "evt.payloadCustody",
          runtime.ExecutionTaskId,
          payload,
          nameof(PayloadHolderType.StationBoundary),
          station.StationId,
          nameof(PayloadHolderType.Device),
          shuttle.DeviceId),
      StationType.Unload => CreateStableIdentifier(
          "evt.payloadCustody",
          runtime.ExecutionTaskId,
          payload,
          nameof(PayloadHolderType.Device),
          shuttle.DeviceId,
          nameof(PayloadHolderType.StationBoundary),
          station.StationId),
      _ => throw new InvalidOperationException($"Unsupported station type '{station.StationType}'.")
    };

    return new StationTransferMaterializationResult(
        StationTransferMaterializationStatus.Completed,
        outboxId: eventId);
  }

  private readonly record struct StationTransferContext(
      ExecutionTaskRuntimeRecord Runtime,
      DeviceShadowRecord Shuttle,
      StationBoundaryStateRecord Station);

  private readonly record struct CustodyTransition(
      string EventId,
      PayloadHolderType PreviousHolderType,
      string PreviousHolderId,
      PayloadHolderType NewHolderType,
      string NewHolderId,
      string? DevicePayloadId,
      string? StationPayloadId,
      DispatchStatus NewDispatchStatus);

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
