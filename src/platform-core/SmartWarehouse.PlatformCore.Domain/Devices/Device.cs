using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Operations;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Devices;

public abstract class Device
{
  protected Device(
      DeviceId deviceId,
      DeviceFamily family,
      NodeId? currentNode,
      DeviceHealthState healthState,
      CapabilitySet capabilities,
      DeviceExecutionState executionState)
  {
    DeviceId = deviceId;
    Family = family;
    CurrentNode = currentNode;
    HealthState = healthState;
    Capabilities = DomainGuard.NotNull(capabilities, nameof(capabilities));
    ExecutionState = executionState;
  }

  public DeviceId DeviceId { get; }

  public DeviceFamily Family { get; }

  public NodeId? CurrentNode { get; }

  public DeviceHealthState HealthState { get; }

  public CapabilitySet Capabilities { get; }

  public IReadOnlyList<CapabilityId> ActiveCapabilities => Capabilities.ActiveCapabilities;

  public DeviceExecutionState ExecutionState { get; }
}
