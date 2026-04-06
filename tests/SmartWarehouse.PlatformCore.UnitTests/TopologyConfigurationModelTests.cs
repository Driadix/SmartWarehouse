using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class TopologyConfigurationModelTests
{
  [Fact]
  public void TravelNodeRejectsStationReference()
  {
    var exception = Assert.Throws<ArgumentException>(() => new TopologyNodeConfig(
        new NodeId("NODE_1"),
        NodeType.TravelNode,
        new LevelId("L1"),
        [],
        stationId: new StationId("LOAD_01")));

    Assert.Contains("cannot reference station", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void ChargeNodeRequiresServicePointIdentifier()
  {
    var exception = Assert.Throws<ArgumentException>(() => new TopologyNodeConfig(
        new NodeId("CHARGE_NODE_1"),
        NodeType.ChargeNode,
        new LevelId("L2"),
        []));

    Assert.Contains("requires a service point identifier", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void CarrierShaftRejectsUnsupportedSlotCount()
  {
    var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new CarrierShaftConfig(
        new ShaftId("LIFT_A"),
        new DeviceId("LIFT_A_DEVICE"),
        2,
        [new CarrierShaftStopConfig(new LevelId("L1"), new NodeId("L1_CARRIER_A"), new NodeId("L1_TP_LIFT_A"))]));

    Assert.Contains("slotCount = 1", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void CarrierShaftRejectsEmptyStops()
  {
    var exception = Assert.Throws<ArgumentException>(() => new CarrierShaftConfig(
        new ShaftId("LIFT_A"),
        new DeviceId("LIFT_A_DEVICE"),
        1,
        []));

    Assert.Contains("cannot be empty", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void CarrierShaftRejectsDuplicateStopLevels()
  {
    var exception = Assert.Throws<ArgumentException>(() => new CarrierShaftConfig(
        new ShaftId("LIFT_A"),
        new DeviceId("LIFT_A_DEVICE"),
        1,
        [
          new CarrierShaftStopConfig(new LevelId("L1"), new NodeId("L1_CARRIER_A"), new NodeId("L1_TP_LIFT_A")),
          new CarrierShaftStopConfig(new LevelId("L1"), new NodeId("L1_CARRIER_B"), new NodeId("L1_TP_LIFT_B"))
        ]));

    Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void EndpointMappingRejectsChargePointWithStationIdentifier()
  {
    var exception = Assert.Throws<ArgumentException>(() => new EndpointMappingConfig(
        new EndpointId("charge.l2.a"),
        EndpointKind.ChargePoint,
        stationId: new StationId("LOAD_01"),
        servicePointId: new ServicePointId("CHARGE_01")));

    Assert.Contains("cannot reference a station", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void WarehouseTopologyConfigRejectsDuplicateEndpointIdentifiers()
  {
    var exception = Assert.Throws<ArgumentException>(() => new WarehouseTopologyConfig(
        new TopologyId("WH-A"),
        1,
        levels: [],
        nodes: [],
        edges: [],
        shafts: [],
        stations: [],
        servicePoints: [],
        deviceBindings: [],
        endpointMappings:
        [
          new EndpointMappingConfig(new EndpointId("inbound.main"), EndpointKind.LoadStation, stationId: new StationId("LOAD_01")),
          new EndpointMappingConfig(new EndpointId("inbound.main"), EndpointKind.LoadStation, stationId: new StationId("LOAD_02"))
        ]));

    Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
  }
}
