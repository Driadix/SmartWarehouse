using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Application.Wcs;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

namespace SmartWarehouse.PlatformCore.Infrastructure.Wcs;

public static class PersistenceWcsNavigateTaskMaterializationServiceCollectionExtensions
{
  public static IServiceCollection AddPersistenceWcsNavigateTaskMaterialization(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddScoped<IWcsNavigateTaskMaterializer, PersistenceWcsNavigateTaskMaterializer>();

    return services;
  }
}

internal sealed class PersistenceWcsNavigateTaskMaterializer(
    PlatformCoreDbContext dbContext,
    CompiledWarehouseTopology topology,
    IWcsOperationalStateStore operationalStateStore) : IWcsNavigateTaskMaterializer
{
  private const string MotionWindowCapability = "motion.windowed";
  private const string ActiveReservationState = "ACTIVE";
  private const string MotionAuthorizedPhase = "MotionAuthorized";
  private const string AwaitingMotionWindowPhase = "AwaitingMotionWindow";
  private const string AwaitingReplanPhase = "AwaitingReplan";
  private const string CompletedPhase = "Completed";

  public async ValueTask<NavigateMaterializationResult> MaterializeAsync(
      ExecutionTaskId executionTaskId,
      CancellationToken cancellationToken = default)
  {
    await operationalStateStore.EnsureInitializedAsync(cancellationToken);

    var runtime = await dbContext.ExecutionTaskRuntime
        .SingleOrDefaultAsync(record => record.ExecutionTaskId == executionTaskId.Value, cancellationToken)
        ?? throw new InvalidOperationException($"Execution task runtime '{executionTaskId}' was not found.");

    if (runtime.TaskType != ExecutionTaskType.Navigate)
    {
      throw new InvalidOperationException($"Execution task '{executionTaskId}' is not a Navigate task.");
    }

    if (!string.Equals(runtime.AssigneeType, nameof(ExecutionActorType.Device), StringComparison.Ordinal))
    {
      throw new InvalidOperationException($"Navigate task '{executionTaskId}' must be assigned to a device.");
    }

    if (string.IsNullOrWhiteSpace(runtime.TargetNodeId))
    {
      throw new InvalidOperationException($"Navigate task '{executionTaskId}' does not define a target node.");
    }

    var targetNodeId = new NodeId(runtime.TargetNodeId);
    var deviceShadow = await dbContext.DeviceShadows
        .SingleOrDefaultAsync(record => record.DeviceId == runtime.AssigneeId, cancellationToken)
        ?? throw new InvalidOperationException($"Device shadow '{runtime.AssigneeId}' was not found for task '{executionTaskId}'.");

    if (deviceShadow.DeviceFamily != DeviceFamily.Shuttle3D)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: AwaitingReplanPhase,
          reasonCode: "UNSUPPORTED_NAVIGATE_ASSIGNEE",
          resolutionHint: ExecutionResolutionHint.ReplanRequired,
          replanRequired: true,
          cancellationToken);
    }

    if (!deviceShadow.ActiveCapabilities.Contains(MotionWindowCapability, StringComparer.Ordinal))
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: AwaitingMotionWindowPhase,
          reasonCode: "MOTION_WINDOW_CAPABILITY_UNAVAILABLE",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    var now = DateTimeOffset.UtcNow;
    var deviceSession = await dbContext.DeviceSessions
        .SingleOrDefaultAsync(
            record => record.DeviceId == runtime.AssigneeId && record.LeaseUntil > now,
            cancellationToken);

    if (runtime.State == ExecutionTaskState.InProgress &&
        string.Equals(runtime.ActiveRuntimePhase, MotionAuthorizedPhase, StringComparison.Ordinal) &&
        deviceSession is not null)
    {
      var existingReservation = await dbContext.Reservations
          .AsNoTracking()
          .SingleOrDefaultAsync(
              record =>
                  record.OwnerType == nameof(ReservationOwnerType.ExecutionTask) &&
                  record.OwnerId == executionTaskId.Value &&
                  record.Horizon == ReservationHorizon.Execution &&
                  record.State == ActiveReservationState,
              cancellationToken);
      var outboxId = CreateStableIdentifier(
          "wcs.navigate",
          executionTaskId.Value,
          runtime.TaskRevision.ToString(CultureInfo.InvariantCulture),
          deviceSession.DeviceSessionId);
      var outboxExists = await dbContext.OutboxMessages
          .AsNoTracking()
          .AnyAsync(record => record.OutboxId == outboxId, cancellationToken);

      if (existingReservation is not null && outboxExists)
      {
        return new NavigateMaterializationResult(
            NavigateMaterializationStatus.AlreadyAuthorized,
            existingReservation.ReservedNodeIds.Select(static nodeId => new NodeId(nodeId)),
            outboxId);
      }
    }

    if (runtime.State != ExecutionTaskState.Planned)
    {
      throw new InvalidOperationException(
          $"Navigate task '{executionTaskId}' cannot be materialized from state '{runtime.State}'.");
    }

    if (deviceSession is null)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: AwaitingMotionWindowPhase,
          reasonCode: "DEVICE_SESSION_REQUIRED",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    if (string.IsNullOrWhiteSpace(deviceShadow.CurrentNodeId))
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: AwaitingMotionWindowPhase,
          reasonCode: "CURRENT_NODE_UNKNOWN",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    var currentNodeId = new NodeId(deviceShadow.CurrentNodeId);
    if (currentNodeId == targetNodeId)
    {
      runtime.State = ExecutionTaskState.Completed;
      runtime.ActiveRuntimePhase = CompletedPhase;
      runtime.ReasonCode = null;
      runtime.ResolutionHint = null;
      runtime.ReplanRequired = null;
      deviceShadow.ExecutionState = DeviceExecutionState.Idle;

      await dbContext.SaveChangesAsync(cancellationToken);

      return new NavigateMaterializationResult(NavigateMaterializationStatus.Completed);
    }

    var fullPath = FindShortestPath(topology, currentNodeId, targetNodeId);
    if (fullPath is null)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: AwaitingReplanPhase,
          reasonCode: "NO_ADMISSIBLE_ROUTE",
          resolutionHint: ExecutionResolutionHint.ReplanRequired,
          replanRequired: true,
          cancellationToken);
    }

    var blockingReservations = await dbContext.Reservations
        .AsNoTracking()
        .Where(record =>
            record.Horizon == ReservationHorizon.Execution &&
            record.State == ActiveReservationState &&
            !(record.OwnerType == nameof(ReservationOwnerType.ExecutionTask) && record.OwnerId == executionTaskId.Value))
        .ToListAsync(cancellationToken);
    var blockedNodeIds = blockingReservations
        .SelectMany(static record => record.ReservedNodeIds)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    var blockedNodeIdSet = blockedNodeIds.ToHashSet(StringComparer.Ordinal);
    var authorizedNodePath = BuildAuthorizedNodePath(topology, fullPath, targetNodeId, blockedNodeIdSet);

    if (authorizedNodePath.Count == 0)
    {
      return await SuspendAsync(
          runtime,
          runtimePhase: AwaitingMotionWindowPhase,
          reasonCode: "MOTION_WINDOW_BLOCKED",
          resolutionHint: ExecutionResolutionHint.WaitAndRetry,
          replanRequired: false,
          cancellationToken);
    }

    var reservationId = CreateStableIdentifier("wcs.reservation.navigate", executionTaskId.Value);
    var reservation = await dbContext.Reservations
        .SingleOrDefaultAsync(record => record.ReservationId == reservationId, cancellationToken);

    if (reservation is null)
    {
      dbContext.Reservations.Add(new ReservationRecord
      {
        ReservationId = reservationId,
        OwnerType = nameof(ReservationOwnerType.ExecutionTask),
        OwnerId = executionTaskId.Value,
        ReservedNodeIds = authorizedNodePath.Select(static nodeId => nodeId.Value).ToArray(),
        Horizon = ReservationHorizon.Execution,
        State = ActiveReservationState
      });
    }
    else
    {
      reservation.OwnerType = nameof(ReservationOwnerType.ExecutionTask);
      reservation.OwnerId = executionTaskId.Value;
      reservation.ReservedNodeIds = authorizedNodePath.Select(static nodeId => nodeId.Value).ToArray();
      reservation.Horizon = ReservationHorizon.Execution;
      reservation.State = ActiveReservationState;
    }

    runtime.State = ExecutionTaskState.InProgress;
    runtime.ActiveRuntimePhase = MotionAuthorizedPhase;
    runtime.ReasonCode = null;
    runtime.ResolutionHint = null;
    runtime.ReplanRequired = null;

    deviceShadow.ExecutionState = DeviceExecutionState.Executing;
    deviceShadow.DispatchStatus = DispatchStatus.Occupied;

    var commandOutboxId = CreateStableIdentifier(
        "wcs.navigate",
        executionTaskId.Value,
        runtime.TaskRevision.ToString(CultureInfo.InvariantCulture),
        deviceSession.DeviceSessionId);
    var existingOutboxRecord = await dbContext.OutboxMessages
        .SingleOrDefaultAsync(record => record.OutboxId == commandOutboxId, cancellationToken);

    if (existingOutboxRecord is null)
    {
      dbContext.OutboxMessages.Add(CreateGrantMotionWindowOutboxRecord(
          commandOutboxId,
          runtime,
          deviceShadow,
          deviceSession,
          authorizedNodePath,
          now));
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    return new NavigateMaterializationResult(
        NavigateMaterializationStatus.MotionAuthorized,
        authorizedNodePath,
        commandOutboxId);
  }

  private static OutboxMessageRecord CreateGrantMotionWindowOutboxRecord(
      string outboxId,
      ExecutionTaskRuntimeRecord runtime,
      DeviceShadowRecord deviceShadow,
      DeviceSessionRecord deviceSession,
      IReadOnlyList<NodeId> authorizedNodePath,
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
          deviceId = deviceShadow.DeviceId,
          family = deviceShadow.DeviceFamily.ToString(),
          sessionId = deviceSession.DeviceSessionId,
          platformTime = createdAt,
          payload = new
          {
            nodePath = authorizedNodePath.Select(static nodeId => nodeId.Value).ToArray()
          }
        });

    return new OutboxMessageRecord
    {
      OutboxId = outboxId,
      Producer = "WCS",
      MessageKind = "INTERNAL_COMMAND",
      AggregateType = "ExecutionTask",
      AggregateId = runtime.ExecutionTaskId,
      CorrelationId = runtime.CorrelationId,
      CausationId = runtime.ExecutionTaskId,
      Payload = payload,
      CreatedAt = createdAt
    };
  }

  private async ValueTask<NavigateMaterializationResult> SuspendAsync(
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

    return new NavigateMaterializationResult(NavigateMaterializationStatus.Suspended);
  }

  private static List<NodeId> BuildAuthorizedNodePath(
      CompiledWarehouseTopology topology,
      IReadOnlyList<NodeId> fullPath,
      NodeId targetNodeId,
      HashSet<string> blockedNodeIds)
  {
    var authorizedNodes = new List<NodeId>();

    foreach (var nodeId in fullPath.Skip(1))
    {
      if (blockedNodeIds.Contains(nodeId.Value))
      {
        break;
      }

      authorizedNodes.Add(nodeId);

      if (IsConflictPoint(topology, nodeId, targetNodeId))
      {
        break;
      }
    }

    return authorizedNodes;
  }

  private static bool IsConflictPoint(CompiledWarehouseTopology topology, NodeId nodeId, NodeId targetNodeId)
  {
    if (nodeId == targetNodeId)
    {
      return true;
    }

    if (!topology.TryGetNode(nodeId, out var node))
    {
      throw new InvalidOperationException($"Node '{nodeId}' is not present in compiled topology '{topology.TopologyId}'.");
    }

    return node.NodeType is NodeType.SwitchNode or NodeType.TransferPoint;
  }

  private static string CreateStableIdentifier(string prefix, params string[] parts)
  {
    var payload = string.Join("|", parts);
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    return $"{prefix}.{hash[..24]}";
  }

  private static List<NodeId>? FindShortestPath(
      CompiledWarehouseTopology topology,
      NodeId sourceNodeId,
      NodeId targetNodeId)
  {
    var frontier = new PriorityQueue<NodeId, decimal>();
    var distances = new Dictionary<NodeId, decimal>
    {
      [sourceNodeId] = 0m
    };
    var previousNodes = new Dictionary<NodeId, NodeId>();

    frontier.Enqueue(sourceNodeId, 0m);

    while (frontier.TryDequeue(out var currentNodeId, out var currentDistance))
    {
      if (distances.TryGetValue(currentNodeId, out var bestKnownDistance) && currentDistance > bestKnownDistance)
      {
        continue;
      }

      if (currentNodeId == targetNodeId)
      {
        return ReconstructPath(previousNodes, sourceNodeId, targetNodeId);
      }

      foreach (var transition in EnumerateTransitions(topology, currentNodeId))
      {
        var nextDistance = currentDistance + transition.Weight;

        if (distances.TryGetValue(transition.NodeId, out var existingDistance) && nextDistance >= existingDistance)
        {
          continue;
        }

        distances[transition.NodeId] = nextDistance;
        previousNodes[transition.NodeId] = currentNodeId;
        frontier.Enqueue(transition.NodeId, nextDistance);
      }
    }

    return null;
  }

  private static List<NodeId> ReconstructPath(
      Dictionary<NodeId, NodeId> previousNodes,
      NodeId sourceNodeId,
      NodeId targetNodeId)
  {
    var path = new List<NodeId> { targetNodeId };

    while (path[^1] != sourceNodeId)
    {
      path.Add(previousNodes[path[^1]]);
    }

    path.Reverse();
    return path;
  }

  private static IEnumerable<RouteTransition> EnumerateTransitions(
      CompiledWarehouseTopology topology,
      NodeId currentNodeId)
  {
    foreach (var edge in topology.GetOutgoingEdges(currentNodeId))
    {
      if (edge.TraversalMode is not (EdgeTraversalMode.Open or EdgeTraversalMode.CarrierOnly))
      {
        continue;
      }

      yield return new RouteTransition(edge.ToNodeId, edge.Weight);
    }

    if (topology.TryGetShaftStopByTransferPoint(currentNodeId, out var stopByTransferPoint))
    {
      yield return new RouteTransition(stopByTransferPoint.CarrierNodeId, 0m);
    }

    if (topology.TryGetShaftStopByCarrierNode(currentNodeId, out var stopByCarrierNode))
    {
      yield return new RouteTransition(stopByCarrierNode.TransferPointId, 0m);
    }
  }

  private readonly record struct RouteTransition(NodeId NodeId, decimal Weight);
}
