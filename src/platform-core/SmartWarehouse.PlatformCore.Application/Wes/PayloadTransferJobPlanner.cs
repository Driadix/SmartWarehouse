using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Execution;
using SmartWarehouse.PlatformCore.Domain.Jobs;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Application.Wes;

public interface IPayloadTransferJobPlanner
{
  Job Plan(
      JobId jobId,
      EndpointId sourceEndpointId,
      EndpointId targetEndpointId,
      JobPriority priority);
}

public sealed class PayloadTransferJobPlanner(
    CompiledWarehouseTopology topology,
    IWarehouseRouteService routeService) : IPayloadTransferJobPlanner
{
  public Job Plan(
      JobId jobId,
      EndpointId sourceEndpointId,
      EndpointId targetEndpointId,
      JobPriority priority)
  {
    var sourceEndpoint = topology.ResolveEndpoint(sourceEndpointId);
    var targetEndpoint = topology.ResolveEndpoint(targetEndpointId);
    var plannedRoute = routeService.ResolveRoute(topology, sourceEndpointId, targetEndpointId);
    var shuttle = SelectShuttleBinding(sourceEndpoint);
    var executionTasks = BuildExecutionTasks(
        jobId,
        sourceEndpoint,
        targetEndpoint,
        shuttle,
        plannedRoute.NodePath);

    return new Job(
        jobId,
        JobType.PayloadTransfer,
        sourceEndpointId,
        targetEndpointId,
        JobState.Planned,
        priority,
        plannedRoute: plannedRoute,
        executionTasks: executionTasks);
  }

  private System.Collections.ObjectModel.ReadOnlyCollection<ExecutionTask> BuildExecutionTasks(
      JobId jobId,
      CompiledEndpointBinding sourceEndpoint,
      CompiledEndpointBinding targetEndpoint,
      CompiledDeviceBinding shuttle,
      IReadOnlyList<NodeId> nodePath)
  {
    var executionTasks = new List<ExecutionTask>();
    var sequenceNo = 1;

    if (sourceEndpoint.BoundaryExecutionResourceRef is { } sourceBoundaryRef)
    {
      executionTasks.Add(CreateStationTransfer(jobId, sequenceNo++, shuttle.ExecutionResourceRef, sourceBoundaryRef, sourceEndpoint.NodeId));
    }

    var navigationStartIndex = 0;
    for (var index = 0; index < nodePath.Count - 1; index++)
    {
      if (!TryReadCarrierTransfer(nodePath, index, out var carrierTargetIndex, out var carrierParticipant))
      {
        continue;
      }

      if (index > navigationStartIndex)
      {
        executionTasks.Add(CreateNavigate(jobId, sequenceNo++, shuttle.ExecutionResourceRef, nodePath[index]));
      }

      executionTasks.Add(CreateCarrierTransfer(
          jobId,
          sequenceNo++,
          shuttle.ExecutionResourceRef,
          carrierParticipant,
          nodePath[index],
          nodePath[carrierTargetIndex]));

      navigationStartIndex = carrierTargetIndex;
      index = carrierTargetIndex;
    }

    var finalTargetIndex = nodePath.Count - 1;
    if (finalTargetIndex > navigationStartIndex)
    {
      executionTasks.Add(CreateNavigate(jobId, sequenceNo++, shuttle.ExecutionResourceRef, nodePath[finalTargetIndex]));
    }

    if (targetEndpoint.BoundaryExecutionResourceRef is { } targetBoundaryRef)
    {
      executionTasks.Add(CreateStationTransfer(jobId, sequenceNo, shuttle.ExecutionResourceRef, targetBoundaryRef, targetEndpoint.NodeId));
    }

    return executionTasks.AsReadOnly();
  }

  private bool TryReadCarrierTransfer(
      IReadOnlyList<NodeId> nodePath,
      int startIndex,
      out int targetIndex,
      out ExecutionResourceRef participantRef)
  {
    targetIndex = default;
    participantRef = default;

    if (!topology.TryGetShaftStopByTransferPoint(nodePath[startIndex], out var sourceStop) ||
        startIndex + 1 >= nodePath.Count ||
        nodePath[startIndex + 1] != sourceStop.CarrierNodeId)
    {
      return false;
    }

    var carrierIndex = startIndex + 1;
    while (carrierIndex + 1 < nodePath.Count &&
           topology.TryGetShaftStopByCarrierNode(nodePath[carrierIndex + 1], out var intermediateStop) &&
           intermediateStop.ShaftId == sourceStop.ShaftId)
    {
      carrierIndex++;
    }

    if (carrierIndex + 1 >= nodePath.Count ||
        !topology.TryGetShaftStopByTransferPoint(nodePath[carrierIndex + 1], out var targetStop) ||
        targetStop.ShaftId != sourceStop.ShaftId)
    {
      return false;
    }

    targetIndex = carrierIndex + 1;
    participantRef = ExecutionResourceRef.ForDevice(sourceStop.CarrierDeviceId);
    return true;
  }

  private CompiledDeviceBinding SelectShuttleBinding(CompiledEndpointBinding sourceEndpoint)
  {
    return topology.DeviceBindings
        .Where(static binding => binding.Family == DeviceFamily.Shuttle3D)
        .OrderBy(binding => SharesLevel(binding, sourceEndpoint) ? 0 : 1)
        .ThenBy(static binding => binding.DeviceId.Value, StringComparer.Ordinal)
        .First();
  }

  private bool SharesLevel(CompiledDeviceBinding binding, CompiledEndpointBinding endpoint)
  {
    return MatchesLevel(binding.InitialNodeId, endpoint.LevelId) ||
           MatchesLevel(binding.HomeNodeId, endpoint.LevelId);
  }

  private bool MatchesLevel(NodeId? nodeId, LevelId? levelId)
  {
    if (nodeId is null || levelId is null || !topology.TryGetNode(nodeId.Value, out var node))
    {
      return false;
    }

    return node.LevelId == levelId;
  }

  private static ExecutionTask CreateNavigate(
      JobId jobId,
      int sequenceNo,
      ExecutionResourceRef assignee,
      NodeId targetNode) =>
      new(
          CreateTaskId(jobId, sequenceNo),
          jobId,
          assignee,
          Array.Empty<ExecutionResourceRef>(),
          ExecutionTaskType.Navigate,
          ExecutionTaskState.Planned,
          CreateCorrelationId(jobId, sequenceNo),
          targetNode: targetNode);

  private static ExecutionTask CreateStationTransfer(
      JobId jobId,
      int sequenceNo,
      ExecutionResourceRef assignee,
      ExecutionResourceRef boundaryRef,
      NodeId targetNode) =>
      new(
          CreateTaskId(jobId, sequenceNo),
          jobId,
          assignee,
          [boundaryRef],
          ExecutionTaskType.StationTransfer,
          ExecutionTaskState.Planned,
          CreateCorrelationId(jobId, sequenceNo),
          targetNode: targetNode);

  private static ExecutionTask CreateCarrierTransfer(
      JobId jobId,
      int sequenceNo,
      ExecutionResourceRef assignee,
      ExecutionResourceRef participantRef,
      NodeId sourceNode,
      NodeId targetNode) =>
      new(
          CreateTaskId(jobId, sequenceNo),
          jobId,
          assignee,
          [participantRef],
          ExecutionTaskType.CarrierTransfer,
          ExecutionTaskState.Planned,
          CreateCorrelationId(jobId, sequenceNo),
          sourceNode: sourceNode,
          targetNode: targetNode,
          transferMode: TransferMode.ShuttleRidesHybridLiftWithPayload);

  private static ExecutionTaskId CreateTaskId(JobId jobId, int sequenceNo) =>
      new($"task-{jobId.Value}-{sequenceNo:D2}");

  private static CorrelationId CreateCorrelationId(JobId jobId, int sequenceNo) =>
      new($"corr-{jobId.Value}-{sequenceNo:D2}");
}

public static class PayloadTransferJobPlannerServiceCollectionExtensions
{
  public static IServiceCollection AddPayloadTransferJobPlanner(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.TryAddSingleton<IPayloadTransferJobPlanner, PayloadTransferJobPlanner>();

    return services;
  }
}
