using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using System.Collections.ObjectModel;

namespace SmartWarehouse.PlatformCore.Application.Wes;

public interface IWarehouseRouteService
{
  PlannedRoute ResolveRoute(
      CompiledWarehouseTopology topology,
      EndpointId sourceEndpointId,
      EndpointId targetEndpointId);
}

public sealed class NoAdmissibleRouteException : InvalidOperationException
{
  public const string ProblemCode = "NO_ADMISSIBLE_ROUTE";

  public NoAdmissibleRouteException(
      TopologyId topologyId,
      EndpointId sourceEndpointId,
      EndpointId targetEndpointId)
      : base($"No admissible route exists between endpoint '{sourceEndpointId}' and '{targetEndpointId}' in topology '{topologyId}'.")
  {
    TopologyId = topologyId;
    SourceEndpointId = sourceEndpointId;
    TargetEndpointId = targetEndpointId;
  }

  public TopologyId TopologyId { get; }

  public EndpointId SourceEndpointId { get; }

  public EndpointId TargetEndpointId { get; }
}

public sealed class WarehouseRouteService : IWarehouseRouteService
{
  private const decimal ImplicitShaftTransitionWeight = 0m;

  public PlannedRoute ResolveRoute(
      CompiledWarehouseTopology topology,
      EndpointId sourceEndpointId,
      EndpointId targetEndpointId)
  {
    ArgumentNullException.ThrowIfNull(topology);

    if (sourceEndpointId == targetEndpointId)
    {
      throw new ArgumentException("Source and target endpoints must be different.", nameof(targetEndpointId));
    }

    var sourceEndpoint = topology.ResolveEndpoint(sourceEndpointId);
    var targetEndpoint = topology.ResolveEndpoint(targetEndpointId);

    if (sourceEndpoint.NodeId == targetEndpoint.NodeId)
    {
      throw CreateNoAdmissibleRouteException(topology, sourceEndpointId, targetEndpointId);
    }

    var nodePath = FindShortestPath(topology, sourceEndpoint.NodeId, targetEndpoint.NodeId);

    return nodePath is not null
        ? new PlannedRoute(nodePath)
        : throw CreateNoAdmissibleRouteException(topology, sourceEndpointId, targetEndpointId);
  }

  private static NoAdmissibleRouteException CreateNoAdmissibleRouteException(
      CompiledWarehouseTopology topology,
      EndpointId sourceEndpointId,
      EndpointId targetEndpointId) =>
      new(topology.TopologyId, sourceEndpointId, targetEndpointId);

  private static ReadOnlyCollection<NodeId>? FindShortestPath(
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

  private static ReadOnlyCollection<NodeId> ReconstructPath(
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
    return Array.AsReadOnly(path.ToArray());
  }

  private static IEnumerable<RouteTransition> EnumerateTransitions(
      CompiledWarehouseTopology topology,
      NodeId currentNodeId)
  {
    foreach (var edge in topology.GetOutgoingEdges(currentNodeId))
    {
      if (!IsAdmissibleTraversalMode(edge.TraversalMode))
      {
        continue;
      }

      yield return new RouteTransition(edge.ToNodeId, edge.Weight);
    }

    if (topology.TryGetShaftStopByTransferPoint(currentNodeId, out var stopByTransferPoint))
    {
      yield return new RouteTransition(stopByTransferPoint.CarrierNodeId, ImplicitShaftTransitionWeight);
    }

    if (topology.TryGetShaftStopByCarrierNode(currentNodeId, out var stopByCarrierNode))
    {
      yield return new RouteTransition(stopByCarrierNode.TransferPointId, ImplicitShaftTransitionWeight);
    }
  }

  private static bool IsAdmissibleTraversalMode(EdgeTraversalMode traversalMode) =>
      traversalMode is EdgeTraversalMode.Open or EdgeTraversalMode.CarrierOnly;

  private readonly record struct RouteTransition(NodeId NodeId, decimal Weight);
}

public static class WesRoutingServiceCollectionExtensions
{
  public static IServiceCollection AddWarehouseRouteService(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.TryAddSingleton<IWarehouseRouteService, WarehouseRouteService>();

    return services;
  }
}
