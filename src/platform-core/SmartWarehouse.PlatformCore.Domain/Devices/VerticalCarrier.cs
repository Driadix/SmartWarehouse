using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Operations;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Devices;

public abstract class VerticalCarrier : Device
{
  protected VerticalCarrier(
      DeviceId deviceId,
      DeviceFamily family,
      NodeId? currentNode,
      DeviceHealthState healthState,
      CapabilitySet capabilities,
      DeviceExecutionState executionState,
      CarrierKind carrierKind,
      int slotCount,
      DeviceId? occupiedShuttleId = null)
      : base(
          deviceId,
          family,
          currentNode,
          healthState,
          capabilities,
          executionState)
  {
    CarrierKind = carrierKind;
    SlotCount = DomainGuard.Positive(slotCount, nameof(slotCount));
    OccupiedShuttleId = occupiedShuttleId;
  }

  public CarrierKind CarrierKind { get; }

  public int SlotCount { get; }

  public DeviceId? OccupiedShuttleId { get; }
}
