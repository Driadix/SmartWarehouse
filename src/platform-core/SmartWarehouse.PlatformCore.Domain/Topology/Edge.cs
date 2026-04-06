using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Topology;

public sealed class Edge
{
  public Edge(
      EdgeId edgeId,
      NodeId fromNode,
      NodeId toNode,
      EdgeTraversalMode traversalMode,
      decimal weight)
  {
    if (fromNode == toNode)
    {
      throw new ArgumentException("Edge must connect two different nodes.", nameof(toNode));
    }

    EdgeId = edgeId;
    FromNode = fromNode;
    ToNode = toNode;
    TraversalMode = traversalMode;
    Weight = DomainGuard.Positive(weight, nameof(weight));
  }

  public EdgeId EdgeId { get; }

  public NodeId FromNode { get; }

  public NodeId ToNode { get; }

  public EdgeTraversalMode TraversalMode { get; }

  public decimal Weight { get; }
}
