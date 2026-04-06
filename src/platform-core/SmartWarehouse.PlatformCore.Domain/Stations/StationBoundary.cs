using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Stations;

public abstract class StationBoundary
{
  protected StationBoundary(
      StationId stationId,
      StationType stationType,
      NodeId attachedNode,
      StationControlMode controlMode,
      StationReadiness readiness,
      int bufferCapacity)
  {
    StationId = stationId;
    StationType = stationType;
    AttachedNode = attachedNode;
    ControlMode = controlMode;
    Readiness = readiness;
    BufferCapacity = DomainGuard.NonNegative(bufferCapacity, nameof(bufferCapacity));
  }

  public StationId StationId { get; }

  public StationType StationType { get; }

  public NodeId AttachedNode { get; }

  public StationControlMode ControlMode { get; }

  public StationReadiness Readiness { get; }

  public int BufferCapacity { get; }
}
