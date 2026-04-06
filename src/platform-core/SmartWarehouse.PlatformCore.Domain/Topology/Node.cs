using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Topology;

public sealed class Node
{
  public Node(NodeId nodeId, NodeType nodeType, int? level = null)
  {
    if (level is < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(level), level, "Level cannot be negative.");
    }

    NodeId = nodeId;
    NodeType = nodeType;
    Level = level;
  }

  public NodeId NodeId { get; }

  public NodeType NodeType { get; }

  public int? Level { get; }
}
