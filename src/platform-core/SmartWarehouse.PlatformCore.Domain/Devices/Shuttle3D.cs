using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Operations;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Devices;

public sealed class Shuttle3D : Device
{
  public Shuttle3D(
      DeviceId deviceId,
      NodeId? currentNode,
      DeviceHealthState healthState,
      CapabilitySet capabilities,
      DeviceExecutionState executionState,
      ShuttleMovementMode movementMode,
      DispatchStatus dispatchStatus,
      DeviceId? carrierId = null,
      PayloadId? carriedPayloadId = null)
      : base(
          deviceId,
          DeviceFamily.Shuttle3D,
          currentNode,
          healthState,
          capabilities,
          executionState)
  {
    if (movementMode == ShuttleMovementMode.CarrierPassenger && carrierId is null)
    {
      throw new ArgumentException("Carrier passenger mode requires a carrier identifier.", nameof(carrierId));
    }

    if (movementMode == ShuttleMovementMode.Autonomous && carrierId is not null)
    {
      throw new ArgumentException("Autonomous mode cannot keep a carrier identifier.", nameof(carrierId));
    }

    if (dispatchStatus == DispatchStatus.Available && executionState != DeviceExecutionState.Idle)
    {
      throw new ArgumentException(
          "A shuttle marked as available must have idle execution state.",
          nameof(executionState));
    }

    if (movementMode == ShuttleMovementMode.CarrierPassenger && dispatchStatus == DispatchStatus.Available)
    {
      throw new ArgumentException(
          "A shuttle riding a carrier cannot be available for dispatch.",
          nameof(dispatchStatus));
    }

    MovementMode = movementMode;
    DispatchStatus = dispatchStatus;
    CarrierId = carrierId;
    CarriedPayloadId = carriedPayloadId;
  }

  public ShuttleMovementMode MovementMode { get; }

  public DispatchStatus DispatchStatus { get; }

  public DeviceId? CarrierId { get; }

  public PayloadId? CarriedPayloadId { get; }
}
