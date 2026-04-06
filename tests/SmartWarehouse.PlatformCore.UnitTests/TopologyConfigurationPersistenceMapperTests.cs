using System.Globalization;
using System.Text.Json;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class TopologyConfigurationPersistenceMapperTests
{
  private readonly YamlWarehouseTopologyConfigLoader _loader = new();

  [Fact]
  public void ToRecordSetPreservesCanonicalTopologyFields()
  {
    var config = _loader.LoadFromFile(GetTopologyFixturePath("warehouse-a.nominal.yaml"));

    var recordSet = TopologyConfigurationPersistenceMapper.ToRecordSet(
        config,
        topologyVersionId: "WH-A:1",
        sourceHash: "sha256:abc123",
        isActive: true,
        activatedAt: DateTimeOffset.Parse("2026-04-06T12:00:00+03:00", CultureInfo.InvariantCulture));

    Assert.Equal("WH-A:1", recordSet.TopologyVersion.TopologyVersionId);
    Assert.Equal("WH-A", recordSet.TopologyVersion.TopologyId);
    Assert.Equal(1, recordSet.TopologyVersion.Version);
    Assert.Single(recordSet.Levels, level => level.LevelId == "L1");
    Assert.Single(recordSet.Levels, level => level.LevelId == "L2");
    Assert.Contains(recordSet.Shafts, shaft => shaft.ShaftId == "LIFT_A" && shaft.CarrierDeviceId == "LIFT_A_DEVICE");
    Assert.Contains(recordSet.ShaftStops, stop => stop.ShaftId == "LIFT_A" && stop.LevelId == "L1" && stop.TransferPointId == "L1_TP_LIFT_A");
    Assert.Contains(recordSet.Nodes, node => node.NodeId == "L1_TP_LIFT_A" && node.ShaftId == "LIFT_A");
    Assert.Contains(recordSet.DeviceBindings, binding => binding.DeviceId == "LIFT_A_DEVICE" && binding.InitialNodeId == "L1_CARRIER_A" && binding.ShaftId == "LIFT_A");
    Assert.Contains(recordSet.EndpointMappings, mapping => mapping.EndpointId == "inbound.main" && mapping.StationId == "LOAD_01");
    Assert.Contains(recordSet.ServicePoints, servicePoint => servicePoint.ServicePointId == "CHARGE_01" && servicePoint.PassiveSemantics == ServicePointPassiveSemantics.ArrivalConfirmsEngagement);
  }

  [Fact]
  public void RecordSetRoundTripRestoresEquivalentTopologyConfiguration()
  {
    var config = _loader.LoadFromFile(GetTopologyFixturePath("warehouse-a.nominal.yaml"));

    var recordSet = TopologyConfigurationPersistenceMapper.ToRecordSet(
        config,
        topologyVersionId: "WH-A:1",
        sourceHash: "sha256:def456",
        isActive: true,
        activatedAt: DateTimeOffset.UtcNow);

    var roundTripped = TopologyConfigurationPersistenceMapper.ToConfiguration(recordSet);

    Assert.Equal(Normalize(config), Normalize(roundTripped));
  }

  private static string Normalize(WarehouseTopologyConfig config)
  {
    var shape = new
    {
      TopologyId = config.TopologyId.Value,
      config.Version,
      Levels = config.Levels
          .OrderBy(static level => level.LevelId.Value, StringComparer.Ordinal)
          .Select(static level => new { LevelId = level.LevelId.Value, level.Ordinal, level.Name }),
      Nodes = config.Nodes
          .OrderBy(static node => node.NodeId.Value, StringComparer.Ordinal)
          .Select(node => new
          {
            NodeId = node.NodeId.Value,
            NodeType = node.NodeType.ToString(),
            LevelId = node.LevelId?.Value,
            Tags = node.Tags.OrderBy(static tag => tag, StringComparer.Ordinal).ToArray(),
            StationId = node.StationId?.Value,
            ShaftId = node.ShaftId?.Value,
            ServicePointId = node.ServicePointId?.Value
          }),
      Edges = config.Edges
          .OrderBy(static edge => edge.EdgeId.Value, StringComparer.Ordinal)
          .Select(static edge => new
          {
            EdgeId = edge.EdgeId.Value,
            FromNodeId = edge.FromNodeId.Value,
            ToNodeId = edge.ToNodeId.Value,
            TraversalMode = edge.TraversalMode.ToString(),
            edge.Weight
          }),
      Shafts = config.Shafts
          .OrderBy(static shaft => shaft.ShaftId.Value, StringComparer.Ordinal)
          .Select(shaft => new
          {
            ShaftId = shaft.ShaftId.Value,
            CarrierDeviceId = shaft.CarrierDeviceId.Value,
            shaft.SlotCount,
            Stops = shaft.Stops
                .OrderBy(static stop => stop.LevelId.Value, StringComparer.Ordinal)
                .Select(static stop => new
                {
                  LevelId = stop.LevelId.Value,
                  CarrierNodeId = stop.CarrierNodeId.Value,
                  TransferPointId = stop.TransferPointId.Value
                })
          }),
      Stations = config.Stations
          .OrderBy(static station => station.StationId.Value, StringComparer.Ordinal)
          .Select(static station => new
          {
            StationId = station.StationId.Value,
            StationType = station.StationType.ToString(),
            ControlMode = station.ControlMode.ToString(),
            AttachedNodeId = station.AttachedNodeId.Value,
            station.BufferCapacity
          }),
      ServicePoints = config.ServicePoints
          .OrderBy(static servicePoint => servicePoint.ServicePointId.Value, StringComparer.Ordinal)
          .Select(static servicePoint => new
          {
            ServicePointId = servicePoint.ServicePointId.Value,
            ServicePointType = servicePoint.ServicePointType.ToString(),
            NodeId = servicePoint.NodeId.Value,
            PassiveSemantics = servicePoint.PassiveSemantics.ToString()
          }),
      DeviceBindings = config.DeviceBindings
          .OrderBy(static binding => binding.DeviceId.Value, StringComparer.Ordinal)
          .Select(static binding => new
          {
            DeviceId = binding.DeviceId.Value,
            Family = binding.Family.ToString(),
            InitialNodeId = binding.InitialNodeId?.Value,
            HomeNodeId = binding.HomeNodeId?.Value,
            ShaftId = binding.ShaftId?.Value
          }),
      EndpointMappings = config.EndpointMappings
          .OrderBy(static mapping => mapping.EndpointId.Value, StringComparer.Ordinal)
          .Select(static mapping => new
          {
            EndpointId = mapping.EndpointId.Value,
            EndpointKind = mapping.EndpointKind.ToString(),
            StationId = mapping.StationId?.Value,
            ServicePointId = mapping.ServicePointId?.Value
          })
    };

    return JsonSerializer.Serialize(shape);
  }

  private static string GetTopologyFixturePath(string fileName) =>
      Path.Combine(TestRepositoryRoot.Get(), "topologies", "phase1", fileName);
}
