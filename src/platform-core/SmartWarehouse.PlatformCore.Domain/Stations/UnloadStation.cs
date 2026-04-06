using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Stations;

public sealed class UnloadStation : StationBoundary
{
  public UnloadStation(
      StationId stationId,
      NodeId attachedNode,
      StationReadiness readiness,
      int bufferCapacity)
      : base(
          stationId,
          StationType.Unload,
          attachedNode,
          StationControlMode.Passive,
          readiness,
          bufferCapacity)
  {
  }
}
