using YamlDotNet.Core;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Domain;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class TopologyConfigurationLoaderTests
{
  private readonly YamlWarehouseTopologyConfigLoader _loader = new();

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void LoaderRejectsEmptyOrWhitespaceYamlContent(string yaml)
  {
    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("cannot be null, empty, or whitespace", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void NominalFixtureLoadsIntoCanonicalTopologyModel()
  {
    var config = _loader.LoadFromFile(GetTopologyFixturePath("warehouse-a.nominal.yaml"));

    Assert.Equal("WH-A", config.TopologyId.Value);
    Assert.Equal(1, config.Version);
    Assert.Equal(2, config.Levels.Count);
    Assert.Equal(9, config.Nodes.Count);
    Assert.Equal(6, config.Edges.Count);
    Assert.Single(config.Shafts);
    Assert.Equal(2, config.Stations.Count);
    Assert.Single(config.ServicePoints);
    Assert.Equal(2, config.DeviceBindings.Count);
    Assert.Equal(3, config.EndpointMappings.Count);

    var liftTransferPoint = Assert.Single(config.Nodes, node => node.NodeId.Value == "L1_TP_LIFT_A");
    Assert.Equal(NodeType.TransferPoint, liftTransferPoint.NodeType);
    Assert.Equal("LIFT_A", liftTransferPoint.ShaftId?.Value);

    var liftShaft = Assert.Single(config.Shafts);
    Assert.Equal("LIFT_A_DEVICE", liftShaft.CarrierDeviceId.Value);
    Assert.Equal(1, liftShaft.SlotCount);
    Assert.Equal(["L1", "L2"], liftShaft.Stops.Select(stop => stop.LevelId.Value).ToArray());

    var chargePoint = Assert.Single(config.ServicePoints);
    Assert.Equal(ServicePointType.Charge, chargePoint.ServicePointType);
    Assert.Equal(ServicePointPassiveSemantics.ArrivalConfirmsEngagement, chargePoint.PassiveSemantics);

    var endpoint = Assert.Single(config.EndpointMappings, mapping => mapping.EndpointId.Value == "charge.l2.a");
    Assert.Equal(EndpointKind.ChargePoint, endpoint.EndpointKind);
    Assert.Equal("CHARGE_01", endpoint.ServicePointId?.Value);
  }

  [Fact]
  public void NoRouteFixtureLoadsWithoutRejectingDisconnectedGraph()
  {
    var config = _loader.LoadFromFile(GetTopologyFixturePath("warehouse-a.no-route.yaml"));

    Assert.Equal("WH-A-NO-ROUTE", config.TopologyId.Value);
    Assert.Equal(2, config.Levels.Count);
    Assert.Equal(3, config.Nodes.Count);
    Assert.Single(config.Edges);
    Assert.Empty(config.Shafts);
    Assert.Empty(config.ServicePoints);
    Assert.Single(config.DeviceBindings);
    Assert.Equal(2, config.EndpointMappings.Count);
  }

  [Fact]
  public void LoaderDefaultsMissingOptionalCollectionsAndTagsToEmptyLists()
  {
    const string yaml =
        """
        topologyId: MINI
        version: 0
        levels:
          - levelId: L1
            ordinal: 0
        nodes:
          - nodeId: NODE_1
            nodeType: TravelNode
        """;

    var config = _loader.Load(yaml);

    Assert.Equal("MINI", config.TopologyId.Value);
    Assert.Single(config.Levels);
    Assert.Single(config.Nodes);
    Assert.Empty(config.Nodes[0].Tags);
    Assert.Empty(config.Edges);
    Assert.Empty(config.Shafts);
    Assert.Empty(config.Stations);
    Assert.Empty(config.ServicePoints);
    Assert.Empty(config.DeviceBindings);
    Assert.Empty(config.EndpointMappings);
  }

  [Fact]
  public void LoaderRejectsMalformedYaml()
  {
    const string yaml =
        """
        topologyId: BROKEN
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: NODE_1
            nodeType TravelNode
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.IsAssignableFrom<YamlException>(exception.InnerException);
  }

  [Fact]
  public void LoaderRejectsMissingRequiredTopologyIdentifier()
  {
    const string yaml =
        """
        version: 1
        nodes:
          - nodeId: A
            nodeType: TravelNode
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("topologyId", exception.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void LoaderRejectsUnknownTraversalMode()
  {
    const string yaml =
        """
        topologyId: UNKNOWN-MODE
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: A
            nodeType: TravelNode
          - nodeId: B
            nodeType: TravelNode
        edges:
          - edgeId: E1
            fromNodeId: A
            toNodeId: B
            traversalMode: SIDEWAYS
            weight: 1
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("traversalMode", exception.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void LoaderRejectsTransferPointWithoutShaftIdentifier()
  {
    const string yaml =
        """
        topologyId: NO-SHAFT
        version: 1
        nodes:
          - nodeId: TP1
            nodeType: TransferPoint
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("shaft", exception.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoaderRejectsDuplicateNodeTags()
  {
    const string yaml =
        """
        topologyId: DUPLICATE-TAGS
        version: 1
        nodes:
          - nodeId: NODE_1
            nodeType: TravelNode
            tags:
              - hot
              - hot
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("duplicate", exception.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoaderRejectsActiveStationControlModeForPhase1()
  {
    const string yaml =
        """
        topologyId: ACTIVE-STATION
        version: 1
        nodes:
          - nodeId: STATION_NODE_1
            nodeType: StationNode
            stationId: LOAD_01
        stations:
          - stationId: LOAD_01
            stationType: LOAD
            controlMode: ACTIVE
            attachedNodeId: STATION_NODE_1
            bufferCapacity: 1
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("passive stations", exception.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoaderRejectsSelfReferencingEdge()
  {
    const string yaml =
        """
        topologyId: SELF-EDGE
        version: 1
        nodes:
          - nodeId: A
            nodeType: TravelNode
        edges:
          - edgeId: E1
            fromNodeId: A
            toNodeId: A
            traversalMode: OPEN
            weight: 1
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("different nodes", exception.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoaderRejectsShaftWithoutStops()
  {
    const string yaml =
        """
        topologyId: SHAFT-NO-STOPS
        version: 1
        shafts:
          - shaftId: LIFT_A
            carrierDeviceId: LIFT_A_DEVICE
            slotCount: 1
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("cannot be empty", exception.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoaderRejectsShaftWithDuplicateStopLevels()
  {
    const string yaml =
        """
        topologyId: SHAFT-DUPLICATE-STOPS
        version: 1
        shafts:
          - shaftId: LIFT_A
            carrierDeviceId: LIFT_A_DEVICE
            slotCount: 1
            stops:
              - levelId: L1
                carrierNodeId: L1_CARRIER_A
                transferPointId: L1_TP_LIFT_A
              - levelId: L1
                carrierNodeId: L1_CARRIER_B
                transferPointId: L1_TP_LIFT_B
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("duplicate", exception.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoaderRejectsHybridLiftBindingWithoutShaft()
  {
    const string yaml =
        """
        topologyId: LIFT-NO-SHAFT
        version: 1
        deviceBindings:
          - deviceId: LIFT_A
            family: HybridLift
            initialNodeId: L1_CARRIER_A
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("shaft", exception.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoaderRejectsShuttleBindingWithShaftReference()
  {
    const string yaml =
        """
        topologyId: SHUTTLE-WITH-SHAFT
        version: 1
        deviceBindings:
          - deviceId: SHUTTLE_01
            family: Shuttle3D
            shaftId: LIFT_A
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("cannot reference a shaft", exception.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoaderRejectsEndpointMappingWithoutRequiredStationIdentifier()
  {
    const string yaml =
        """
        topologyId: ENDPOINT-ERROR
        version: 1
        endpointMappings:
          - endpointId: inbound.main
            endpointKind: LOAD_STATION
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("station identifier", exception.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoaderRejectsLoadStationEndpointMappedToServicePoint()
  {
    const string yaml =
        """
        topologyId: ENDPOINT-WRONG-TARGET
        version: 1
        endpointMappings:
          - endpointId: inbound.main
            endpointKind: LOAD_STATION
            stationId: LOAD_01
            servicePointId: CHARGE_01
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("cannot reference a service point", exception.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoaderRejectsDuplicateNodeIdentifiers()
  {
    const string yaml =
        """
        topologyId: DUPLICATE-NODES
        version: 1
        nodes:
          - nodeId: NODE_1
            nodeType: TravelNode
          - nodeId: NODE_1
            nodeType: SwitchNode
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("duplicate", exception.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoaderRejectsWhitespaceOptionalIdentifiers()
  {
    const string yaml =
        """
        topologyId: BLANK-OPTIONAL
        version: 1
        endpointMappings:
          - endpointId: service.main
            endpointKind: SERVICE_POINT
            servicePointId: "   "
        """;

    var exception = Assert.Throws<TopologyConfigurationException>(() => _loader.Load(yaml));

    Assert.Contains("cannot be whitespace", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void LoadFromFileThrowsWhenFixtureIsMissing()
  {
    Assert.Throws<FileNotFoundException>(() => _loader.LoadFromFile(GetTopologyFixturePath("missing.yaml")));
  }

  private static string GetTopologyFixturePath(string fileName) =>
      Path.Combine(TestRepositoryRoot.Get(), "topologies", "phase1", fileName);
}
