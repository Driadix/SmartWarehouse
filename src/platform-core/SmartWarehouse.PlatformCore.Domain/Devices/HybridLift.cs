using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Operations;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Devices;

public sealed class HybridLift : VerticalCarrier
{
  public HybridLift(
      DeviceId deviceId,
      NodeId? currentNode,
      DeviceHealthState healthState,
      CapabilitySet capabilities,
      DeviceExecutionState executionState,
      DeviceId? occupiedShuttleId = null)
      : base(
          deviceId,
          DeviceFamily.HybridLift,
          currentNode,
          healthState,
          capabilities,
          executionState,
          CarrierKind.HybridLift,
          slotCount: 1,
          occupiedShuttleId)
  {
  }
}
