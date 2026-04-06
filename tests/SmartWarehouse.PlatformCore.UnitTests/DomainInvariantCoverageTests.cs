using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Devices;
using SmartWarehouse.PlatformCore.Domain.Operations;
using SmartWarehouse.PlatformCore.Domain.Payloads;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class DomainInvariantCoverageTests
{
  [Fact]
  public void PayloadRejectsNonPositiveWeight()
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => new Payload(
        new PayloadId("payload-01"),
        new PayloadKind("EURO_PALLET"),
        new Dimensions(1.2m, 0.8m, 1.5m),
        0m,
        PayloadCustodyHolder.ForStationBoundary(new StationId("LOAD_01"))));
  }

  [Fact]
  public void PayloadPreservesDimensionsAndPhysicalCustodyHolder()
  {
    var payload = new Payload(
        new PayloadId("payload-01"),
        new PayloadKind("EURO_PALLET"),
        new Dimensions(1.2m, 0.8m, 1.5m),
        250m,
        PayloadCustodyHolder.ForDevice(new DeviceId("SHUTTLE_01")));

    Assert.Equal("payload-01", payload.PayloadId.Value);
    Assert.Equal("EURO_PALLET", payload.PayloadKind.Value);
    Assert.Equal(1.2m, payload.Dimensions.Length);
    Assert.Equal(250m, payload.Weight);
    Assert.Equal(PayloadHolderType.Device, payload.CustodyHolder.HolderType);
    Assert.Equal("SHUTTLE_01", payload.CustodyHolder.HolderId);
  }

  [Fact]
  public void ReservationRejectsEmptyNodeSet()
  {
    Assert.Throws<ArgumentException>(() => new Reservation(
        new ReservationId("reservation-01"),
        ReservationOwnerRef.ForJob(new JobId("job-01")),
        [],
        ReservationHorizon.Execution,
        new ReservationState("ACTIVE")));
  }

  [Fact]
  public void ReservationRejectsDuplicateNodes()
  {
    Assert.Throws<ArgumentException>(() => new Reservation(
        new ReservationId("reservation-01"),
        ReservationOwnerRef.ForExecutionTask(new ExecutionTaskId("task-01")),
        [new NodeId("L1_A"), new NodeId("L1_A")],
        ReservationHorizon.Plan,
        new ReservationState("ACTIVE")));
  }

  [Fact]
  public void ReservationPreservesOwnerAndReservedNodes()
  {
    var reservation = new Reservation(
        new ReservationId("reservation-01"),
        ReservationOwnerRef.ForExecutionTask(new ExecutionTaskId("task-01")),
        [new NodeId("L1_A"), new NodeId("L1_B")],
        ReservationHorizon.Execution,
        new ReservationState("ACTIVE"));

    Assert.Equal(ReservationOwnerType.ExecutionTask, reservation.Owner.OwnerType);
    Assert.Equal("task-01", reservation.Owner.OwnerId);
    Assert.Equal(["L1_A", "L1_B"], reservation.Nodes.Select(static node => node.Value).ToArray());
    Assert.Equal(ReservationHorizon.Execution, reservation.Horizon);
    Assert.Equal("ACTIVE", reservation.State.Value);
  }

  [Fact]
  public void DeviceSessionRejectsHeartbeatBeyondLease()
  {
    var leaseUntil = DateTimeOffset.Parse("2026-04-06T12:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture);
    var heartbeatAt = leaseUntil.AddSeconds(1);

    Assert.Throws<ArgumentException>(() => new DeviceSession(
        new DeviceSessionId("session-01"),
        new DeviceId("SHUTTLE_01"),
        new DeviceSessionState("LEASED"),
        leaseUntil,
        heartbeatAt));
  }

  [Fact]
  public void DeviceSessionPreservesLeaseSemanticsWhenHeartbeatIsWithinLease()
  {
    var leaseUntil = DateTimeOffset.Parse("2026-04-06T12:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture);
    var heartbeatAt = leaseUntil.AddSeconds(-5);
    var session = new DeviceSession(
        new DeviceSessionId("session-01"),
        new DeviceId("SHUTTLE_01"),
        new DeviceSessionState("LEASED"),
        leaseUntil,
        heartbeatAt);

    Assert.Equal("session-01", session.SessionId.Value);
    Assert.Equal("SHUTTLE_01", session.DeviceId.Value);
    Assert.Equal("LEASED", session.State.Value);
    Assert.Equal(leaseUntil, session.LeaseUntil);
    Assert.Equal(heartbeatAt, session.LastHeartbeatAt);
  }

  [Fact]
  public void FaultPreservesSourceSeverityAndState()
  {
    var fault = new Fault(
        new FaultId("fault-01"),
        FaultSourceRef.ForStationBoundary(new StationId("LOAD_01")),
        new FaultCode("STATION_BLOCKED"),
        new FaultSeverity("MAJOR"),
        FaultState.Active);

    Assert.Equal("fault-01", fault.FaultId.Value);
    Assert.Equal(FaultSourceType.StationBoundary, fault.Source.SourceType);
    Assert.Equal("LOAD_01", fault.Source.SourceId);
    Assert.Equal("STATION_BLOCKED", fault.FaultCode.Value);
    Assert.Equal("MAJOR", fault.Severity.Value);
    Assert.Equal(FaultState.Active, fault.State);
  }

  [Fact]
  public void HybridLiftCreatesSingleSlotVerticalCarrier()
  {
    var lift = new HybridLift(
        new DeviceId("LIFT_A_DEVICE"),
        new NodeId("L1_CARRIER_A"),
        new DeviceHealthState("HEALTHY"),
        CreateCapabilities(),
        DeviceExecutionState.Idle,
        occupiedShuttleId: new DeviceId("SHUTTLE_01"));

    Assert.Equal(DeviceFamily.HybridLift, lift.Family);
    Assert.Equal(CarrierKind.HybridLift, lift.CarrierKind);
    Assert.Equal(1, lift.SlotCount);
    Assert.Equal("L1_CARRIER_A", lift.CurrentNode?.Value);
    Assert.Equal("SHUTTLE_01", lift.OccupiedShuttleId?.Value);
    Assert.Equal(["session.lease", "motion.windowed"], lift.ActiveCapabilities.Select(static capability => capability.Value).ToArray());
  }

  private static CapabilitySet CreateCapabilities() =>
      new(
          [new CapabilityId("session.lease"), new CapabilityId("motion.windowed")],
          [new CapabilityId("session.lease"), new CapabilityId("motion.windowed")]);
}
