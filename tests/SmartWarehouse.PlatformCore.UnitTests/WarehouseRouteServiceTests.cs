using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Application.Wes;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class WarehouseRouteServiceTests
{
  private readonly YamlWarehouseTopologyConfigLoader _loader = new();
  private readonly WarehouseTopologyCompiler _compiler = new(new WarehouseTopologyConfigValidator());
  private readonly WarehouseRouteService _routeService = new();

  [Fact]
  public void RouteServiceBuildsRouteAcrossLevelGraphAndCarrierShaft()
  {
    var topology = CompileFixture("warehouse-a.nominal.yaml");

    var route = _routeService.ResolveRoute(
        topology,
        new EndpointId("inbound.main"),
        new EndpointId("outbound.main"));

    Assert.Equal(
        [
            "L1_LOAD_01",
            "L1_TRAVEL_001",
            "L1_SWITCH_A",
            "L1_TP_LIFT_A",
            "L1_CARRIER_A",
            "L2_CARRIER_A",
            "L2_TP_LIFT_A",
            "L2_UNLOAD_01"
        ],
        route.NodePath.Select(static nodeId => nodeId.Value).ToArray());
  }

  [Fact]
  public void RouteServiceThrowsNormativeNoAdmissibleRouteForDisconnectedFixture()
  {
    var topology = CompileFixture("warehouse-a.no-route.yaml");

    var exception = Assert.Throws<NoAdmissibleRouteException>(() => _routeService.ResolveRoute(
        topology,
        new EndpointId("inbound.main"),
        new EndpointId("outbound.main")));

    Assert.Equal("WH-A-NO-ROUTE", exception.TopologyId.Value);
    Assert.Equal("inbound.main", exception.SourceEndpointId.Value);
    Assert.Equal("outbound.main", exception.TargetEndpointId.Value);
  }

  [Fact]
  public void RouteServicePrefersLowestTotalWeightEvenWhenRouteHasMoreHops()
  {
    var topology = CompileYaml(
        """
        topologyId: WEIGHTED
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: LOAD_NODE
            nodeType: StationNode
            levelId: L1
            stationId: LOAD_01
          - nodeId: FAST_A
            nodeType: TravelNode
            levelId: L1
          - nodeId: FAST_B
            nodeType: TravelNode
            levelId: L1
          - nodeId: SLOW_A
            nodeType: TravelNode
            levelId: L1
          - nodeId: UNLOAD_NODE
            nodeType: StationNode
            levelId: L1
            stationId: UNLOAD_01
        edges:
          - edgeId: E1
            fromNodeId: LOAD_NODE
            toNodeId: SLOW_A
            traversalMode: OPEN
            weight: 1
          - edgeId: E2
            fromNodeId: SLOW_A
            toNodeId: UNLOAD_NODE
            traversalMode: OPEN
            weight: 10
          - edgeId: E3
            fromNodeId: LOAD_NODE
            toNodeId: FAST_A
            traversalMode: OPEN
            weight: 1
          - edgeId: E4
            fromNodeId: FAST_A
            toNodeId: FAST_B
            traversalMode: OPEN
            weight: 1
          - edgeId: E5
            fromNodeId: FAST_B
            toNodeId: UNLOAD_NODE
            traversalMode: OPEN
            weight: 1
        shafts: []
        stations:
          - stationId: LOAD_01
            stationType: LOAD
            controlMode: PASSIVE
            attachedNodeId: LOAD_NODE
            bufferCapacity: 1
          - stationId: UNLOAD_01
            stationType: UNLOAD
            controlMode: PASSIVE
            attachedNodeId: UNLOAD_NODE
            bufferCapacity: 1
        servicePoints: []
        deviceBindings: []
        endpointMappings:
          - endpointId: inbound.main
            endpointKind: LOAD_STATION
            stationId: LOAD_01
          - endpointId: outbound.main
            endpointKind: UNLOAD_STATION
            stationId: UNLOAD_01
        """);

    var route = _routeService.ResolveRoute(
        topology,
        new EndpointId("inbound.main"),
        new EndpointId("outbound.main"));

    Assert.Equal(
        ["LOAD_NODE", "FAST_A", "FAST_B", "UNLOAD_NODE"],
        route.NodePath.Select(static nodeId => nodeId.Value).ToArray());
  }

  [Fact]
  public void RouteServiceIgnoresRestrictedEdgesWhenAnOpenAlternativeExists()
  {
    var topology = CompileYaml(
        """
        topologyId: RESTRICTED-BYPASS
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: LOAD_NODE
            nodeType: StationNode
            levelId: L1
            stationId: LOAD_01
          - nodeId: RESTRICTED_A
            nodeType: TravelNode
            levelId: L1
          - nodeId: RESTRICTED_B
            nodeType: TravelNode
            levelId: L1
          - nodeId: OPEN_A
            nodeType: TravelNode
            levelId: L1
          - nodeId: UNLOAD_NODE
            nodeType: StationNode
            levelId: L1
            stationId: UNLOAD_01
        edges:
          - edgeId: E1
            fromNodeId: LOAD_NODE
            toNodeId: RESTRICTED_A
            traversalMode: OPEN
            weight: 1
          - edgeId: E2
            fromNodeId: RESTRICTED_A
            toNodeId: RESTRICTED_B
            traversalMode: RESTRICTED
            weight: 1
          - edgeId: E3
            fromNodeId: RESTRICTED_B
            toNodeId: UNLOAD_NODE
            traversalMode: OPEN
            weight: 1
          - edgeId: E4
            fromNodeId: LOAD_NODE
            toNodeId: OPEN_A
            traversalMode: OPEN
            weight: 4
          - edgeId: E5
            fromNodeId: OPEN_A
            toNodeId: UNLOAD_NODE
            traversalMode: OPEN
            weight: 4
        shafts: []
        stations:
          - stationId: LOAD_01
            stationType: LOAD
            controlMode: PASSIVE
            attachedNodeId: LOAD_NODE
            bufferCapacity: 1
          - stationId: UNLOAD_01
            stationType: UNLOAD
            controlMode: PASSIVE
            attachedNodeId: UNLOAD_NODE
            bufferCapacity: 1
        servicePoints: []
        deviceBindings: []
        endpointMappings:
          - endpointId: inbound.main
            endpointKind: LOAD_STATION
            stationId: LOAD_01
          - endpointId: outbound.main
            endpointKind: UNLOAD_STATION
            stationId: UNLOAD_01
        """);

    var route = _routeService.ResolveRoute(
        topology,
        new EndpointId("inbound.main"),
        new EndpointId("outbound.main"));

    Assert.Equal(
        ["LOAD_NODE", "OPEN_A", "UNLOAD_NODE"],
        route.NodePath.Select(static nodeId => nodeId.Value).ToArray());
  }

  [Fact]
  public void RouteServiceThrowsNoAdmissibleRouteWhenPathRequiresRestrictedEdge()
  {
    var topology = CompileYaml(
        """
        topologyId: RESTRICTED-ONLY
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: LOAD_NODE
            nodeType: StationNode
            levelId: L1
            stationId: LOAD_01
          - nodeId: RESTRICTED_A
            nodeType: TravelNode
            levelId: L1
          - nodeId: RESTRICTED_B
            nodeType: TravelNode
            levelId: L1
          - nodeId: UNLOAD_NODE
            nodeType: StationNode
            levelId: L1
            stationId: UNLOAD_01
        edges:
          - edgeId: E1
            fromNodeId: LOAD_NODE
            toNodeId: RESTRICTED_A
            traversalMode: OPEN
            weight: 1
          - edgeId: E2
            fromNodeId: RESTRICTED_A
            toNodeId: RESTRICTED_B
            traversalMode: RESTRICTED
            weight: 1
          - edgeId: E3
            fromNodeId: RESTRICTED_B
            toNodeId: UNLOAD_NODE
            traversalMode: OPEN
            weight: 1
        shafts: []
        stations:
          - stationId: LOAD_01
            stationType: LOAD
            controlMode: PASSIVE
            attachedNodeId: LOAD_NODE
            bufferCapacity: 1
          - stationId: UNLOAD_01
            stationType: UNLOAD
            controlMode: PASSIVE
            attachedNodeId: UNLOAD_NODE
            bufferCapacity: 1
        servicePoints: []
        deviceBindings: []
        endpointMappings:
          - endpointId: inbound.main
            endpointKind: LOAD_STATION
            stationId: LOAD_01
          - endpointId: outbound.main
            endpointKind: UNLOAD_STATION
            stationId: UNLOAD_01
        """);

    var exception = Assert.Throws<NoAdmissibleRouteException>(() => _routeService.ResolveRoute(
        topology,
        new EndpointId("inbound.main"),
        new EndpointId("outbound.main")));

    Assert.Equal("RESTRICTED-ONLY", exception.TopologyId.Value);
    Assert.Equal("inbound.main", exception.SourceEndpointId.Value);
    Assert.Equal("outbound.main", exception.TargetEndpointId.Value);
  }

  [Fact]
  public void RouteServiceRejectsEquivalentAliasesThatResolveToSameNode()
  {
    var topology = CompileYaml(
        """
        topologyId: SAME-NODE
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: LOAD_NODE
            nodeType: StationNode
            levelId: L1
            stationId: LOAD_01
        edges: []
        shafts: []
        stations:
          - stationId: LOAD_01
            stationType: LOAD
            controlMode: PASSIVE
            attachedNodeId: LOAD_NODE
            bufferCapacity: 1
        servicePoints: []
        deviceBindings: []
        endpointMappings:
          - endpointId: inbound.main
            endpointKind: LOAD_STATION
            stationId: LOAD_01
          - endpointId: inbound.backup
            endpointKind: LOAD_STATION
            stationId: LOAD_01
        """);

    var exception = Assert.Throws<NoAdmissibleRouteException>(() => _routeService.ResolveRoute(
        topology,
        new EndpointId("inbound.main"),
        new EndpointId("inbound.backup")));

  }

  [Fact]
  public void RouteServicePreservesSeparateValidationForUnknownEndpoints()
  {
    var topology = CompileFixture("warehouse-a.nominal.yaml");

    var exception = Assert.Throws<KeyNotFoundException>(() => _routeService.ResolveRoute(
        topology,
        new EndpointId("missing.endpoint"),
        new EndpointId("outbound.main")));

    Assert.Contains("missing.endpoint", exception.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void RouteServiceRegistrationAddsReusableSingleton()
  {
    var services = new ServiceCollection();
    services.AddWarehouseRouteService();

    using var provider = services.BuildServiceProvider();

    var routeService = provider.GetRequiredService<IWarehouseRouteService>();

    Assert.IsType<WarehouseRouteService>(routeService);
    Assert.Same(routeService, provider.GetRequiredService<IWarehouseRouteService>());
  }

  private CompiledWarehouseTopology CompileFixture(string fileName) =>
      _compiler.Compile(_loader.LoadFromFile(GetTopologyFixturePath(fileName)));

  private CompiledWarehouseTopology CompileYaml(string yaml) =>
      _compiler.Compile(_loader.Load(yaml));

  private static string GetTopologyFixturePath(string fileName) =>
      Path.Combine(TestRepositoryRoot.Get(), "topologies", "phase1", fileName);
}
