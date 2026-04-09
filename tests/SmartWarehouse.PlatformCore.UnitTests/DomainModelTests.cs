using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Devices;
using SmartWarehouse.PlatformCore.Domain.Execution;
using SmartWarehouse.PlatformCore.Domain.Jobs;
using SmartWarehouse.PlatformCore.Domain.Operations;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Domain.Stations;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class DomainModelTests
{
  [Fact]
  public void JobIdRejectsWhitespace()
  {
    Assert.Throws<ArgumentException>(() => new JobId("   "));
  }

  [Fact]
  public void JobRequiresDistinctEndpoints()
  {
    Assert.Throws<ArgumentException>(() => new Job(
        new JobId("job-01"),
        JobType.PayloadTransfer,
        new EndpointId("inbound.main"),
        new EndpointId("inbound.main"),
        JobState.Accepted,
        JobPriority.Normal));
  }

  [Fact]
  public void JobRejectsExecutionTasksForAnotherJob()
  {
    var executionTask = new ExecutionTask(
        new ExecutionTaskId("task-01"),
        new JobId("job-02"),
        ExecutionResourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
        [],
        ExecutionTaskType.Navigate,
        ExecutionTaskState.Planned,
        new CorrelationId("corr-01"),
        targetNode: new NodeId("L1_TARGET"));

    Assert.Throws<ArgumentException>(() => new Job(
        new JobId("job-01"),
        JobType.PayloadTransfer,
        new EndpointId("inbound.main"),
        new EndpointId("outbound.main"),
        JobState.Planned,
        JobPriority.Normal,
        executionTasks: [executionTask]));
  }

  [Fact]
  public void PlannedRouteRequiresAtLeastOneNode()
  {
    Assert.Throws<ArgumentException>(() => new PlannedRoute([]));
  }

  [Fact]
  public void CapabilitySetRequiresActiveCapabilitiesToBeStaticSubset()
  {
    Assert.Throws<ArgumentException>(() => new CapabilitySet(
        [new CapabilityId("session.lease")],
        [new CapabilityId("session.lease"), new CapabilityId("motion.windowed")]));
  }

  [Fact]
  public void ShuttlePassengerModeRequiresCarrierId()
  {
    Assert.Throws<ArgumentException>(() => new Shuttle3D(
        new DeviceId("SHUTTLE_01"),
        new NodeId("L1_TRAVEL_001"),
        new DeviceHealthState("HEALTHY"),
        CreateCapabilities(),
        DeviceExecutionState.Executing,
        ShuttleMovementMode.CarrierPassenger,
        DispatchStatus.Occupied));
  }

  [Fact]
  public void ShuttleAvailableStatusRequiresIdleExecutionState()
  {
    Assert.Throws<ArgumentException>(() => new Shuttle3D(
        new DeviceId("SHUTTLE_01"),
        new NodeId("L1_TRAVEL_001"),
        new DeviceHealthState("HEALTHY"),
        CreateCapabilities(),
        DeviceExecutionState.Executing,
        ShuttleMovementMode.Autonomous,
        DispatchStatus.Available));
  }

  [Fact]
  public void CarrierTransferRequiresTransferMode()
  {
    Assert.Throws<ArgumentException>(() => new ExecutionTask(
        new ExecutionTaskId("task-01"),
        new JobId("job-01"),
        ExecutionResourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
        [ExecutionResourceRef.ForDevice(new DeviceId("LIFT_A_DEVICE"))],
        ExecutionTaskType.CarrierTransfer,
        ExecutionTaskState.Planned,
        new CorrelationId("corr-01"),
        sourceNode: new NodeId("L1_TRANSFER_A"),
        targetNode: new NodeId("L2_TRANSFER_A")));
  }

  [Fact]
  public void Phase1StationsArePassiveBoundaries()
  {
    var loadStation = new LoadStation(
        new StationId("LOAD_A"),
        new NodeId("L1_STATION_LOAD"),
        StationReadiness.Ready,
        bufferCapacity: 2);
    var unloadStation = new UnloadStation(
        new StationId("UNLOAD_A"),
        new NodeId("L4_STATION_UNLOAD"),
        StationReadiness.Blocked,
        bufferCapacity: 1);

    Assert.Equal(StationControlMode.Passive, loadStation.ControlMode);
    Assert.Equal(StationType.Load, loadStation.StationType);
    Assert.Equal(StationControlMode.Passive, unloadStation.ControlMode);
    Assert.Equal(StationType.Unload, unloadStation.StationType);
  }

  private static CapabilitySet CreateCapabilities() =>
      new(
          [new CapabilityId("session.lease"), new CapabilityId("motion.windowed")],
          [new CapabilityId("session.lease"), new CapabilityId("motion.windowed")]);
}
