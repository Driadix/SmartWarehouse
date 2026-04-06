namespace SmartWarehouse.PlatformCore.Domain.Primitives;

public sealed class PlannedRoute
{
  public PlannedRoute(IEnumerable<NodeId> nodePath)
  {
    NodePath = DomainGuard.ReadOnlyList(nodePath, nameof(nodePath), allowEmpty: false);
  }

  public IReadOnlyList<NodeId> NodePath { get; }
}
