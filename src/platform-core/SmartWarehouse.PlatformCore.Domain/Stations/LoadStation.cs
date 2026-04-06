using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Stations;

public sealed class LoadStation : StationBoundary
{
  public LoadStation(
      StationId stationId,
      NodeId attachedNode,
      StationReadiness readiness,
      int bufferCapacity)
      : base(
          stationId,
          StationType.Load,
          attachedNode,
          StationControlMode.Passive,
          readiness,
          bufferCapacity)
  {
  }
}
