using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Domain.Stations;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class TopologyCompilerTests
{
  private readonly YamlWarehouseTopologyConfigLoader _loader = new();
  private readonly WarehouseTopologyCompiler _compiler = new(new WarehouseTopologyConfigValidator());

  [Fact]
  public void CompilerBuildsRichRuntimeTopologyForNominalFixture()
  {
    var topology = CompileFixture("warehouse-a.nominal.yaml");

    Assert.Equal("WH-A", topology.TopologyId.Value);
    Assert.Equal(1, topology.Version);
    Assert.Equal(2, topology.Levels.Count);
    Assert.Equal(9, topology.Nodes.Count);
    Assert.Equal(6, topology.Edges.Count);
    Assert.Single(topology.Shafts);
    Assert.Equal(2, topology.Stations.Count);
    Assert.Single(topology.ServicePoints);
    Assert.Equal(2, topology.DeviceBindings.Count);
    Assert.Equal(3, topology.EndpointBindings.Count);

    var inbound = topology.ResolveEndpoint(new EndpointId("inbound.main"));
    Assert.Equal(EndpointKind.LoadStation, inbound.EndpointKind);
    Assert.Equal("L1_LOAD_01", inbound.NodeId.Value);
    Assert.Equal("LOAD_01", inbound.StationId?.Value);
    Assert.NotNull(inbound.StationBoundary);
    Assert.Null(inbound.ServicePoint);
    Assert.Equal(ExecutionActorType.StationBoundary, inbound.BoundaryExecutionResourceRef?.Type);
    Assert.Equal("LOAD_01", inbound.BoundaryExecutionResourceRef?.ResourceId);
    Assert.Equal(PayloadHolderType.StationBoundary, inbound.BoundaryPayloadCustodyHolder?.HolderType);
    Assert.Equal("LOAD_01", inbound.BoundaryPayloadCustodyHolder?.HolderId);

    var loadBoundary = Assert.IsType<LoadStation>(inbound.StationBoundary!.CreateBoundary(StationReadiness.Ready));
    Assert.Equal(StationControlMode.Passive, loadBoundary.ControlMode);
    Assert.Equal("L1_LOAD_01", loadBoundary.AttachedNode.Value);

    var outbound = topology.ResolveEndpoint(new EndpointId("outbound.main"));
    Assert.Equal(EndpointKind.UnloadStation, outbound.EndpointKind);
    Assert.Equal("UNLOAD_01", outbound.StationId?.Value);
    var unloadBoundary = Assert.IsType<UnloadStation>(outbound.StationBoundary!.CreateBoundary(StationReadiness.Maintenance));
    Assert.Equal(StationReadiness.Maintenance, unloadBoundary.Readiness);

    var charge = topology.ResolveEndpoint(new EndpointId("charge.l2.a"));
    Assert.Equal(EndpointKind.ChargePoint, charge.EndpointKind);
    Assert.Equal("CHARGE_01", charge.ServicePointId?.Value);
    Assert.Equal("L2_CHARGE_01", charge.NodeId.Value);
    Assert.Null(charge.StationBoundary);
    Assert.NotNull(charge.ServicePoint);
    Assert.Equal(ServicePointType.Charge, charge.ServicePoint.ServicePointType);

    Assert.True(topology.TryGetStationByAttachedNode(new NodeId("L1_LOAD_01"), out var stationByNode));
    Assert.Same(inbound.StationBoundary, stationByNode);
    Assert.Contains(new EndpointId("inbound.main"), stationByNode.EndpointIds);

    Assert.True(topology.TryGetServicePointByNode(new NodeId("L2_CHARGE_01"), out var servicePointByNode));
    Assert.Same(charge.ServicePoint, servicePointByNode);
    Assert.Contains(new EndpointId("charge.l2.a"), servicePointByNode.EndpointIds);

    Assert.True(topology.TryGetShaft(new ShaftId("LIFT_A"), out var shaft));
    Assert.Equal("LIFT_A_DEVICE", shaft.CarrierDeviceId.Value);
    Assert.Equal(2, shaft.Stops.Count);
    Assert.True(shaft.TryGetStop(new LevelId("L1"), out var level1Stop));
    Assert.Equal("L1_CARRIER_A", level1Stop.CarrierNodeId.Value);
    Assert.Equal("L1_TP_LIFT_A", level1Stop.TransferPointId.Value);

    Assert.True(topology.TryGetShaftStopByTransferPoint(new NodeId("L1_TP_LIFT_A"), out var stopByTransferPoint));
    Assert.Equal("LIFT_A", stopByTransferPoint.ShaftId.Value);
    Assert.Equal("LIFT_A_DEVICE", stopByTransferPoint.CarrierDeviceId.Value);
    Assert.Equal("L1_CARRIER_A", stopByTransferPoint.CarrierNodeId.Value);

    Assert.True(topology.TryGetShaftStopByCarrierNode(new NodeId("L2_CARRIER_A"), out var stopByCarrierNode));
    Assert.Equal("L2_TP_LIFT_A", stopByCarrierNode.TransferPointId.Value);

    Assert.True(topology.TryGetDeviceBinding(new DeviceId("SHUTTLE_01"), out var shuttleBinding));
    Assert.Equal(DeviceFamily.Shuttle3D, shuttleBinding.Family);
    Assert.Equal("L1_TRAVEL_001", shuttleBinding.InitialNodeId?.Value);
    Assert.Equal(ExecutionActorType.Device, shuttleBinding.ExecutionResourceRef.Type);

    Assert.Equal(["E3"], topology.GetOutgoingEdges(new NodeId("L1_SWITCH_A")).Select(static edge => edge.EdgeId.Value).ToArray());
    Assert.Equal(["E4"], topology.GetIncomingEdges(new NodeId("L2_CARRIER_A")).Select(static edge => edge.EdgeId.Value).ToArray());
  }

  [Fact]
  public void CompilerKeepsDisconnectedYetValidTopologyCompilable()
  {
    var topology = CompileFixture("warehouse-a.no-route.yaml");

    Assert.Equal("WH-A-NO-ROUTE", topology.TopologyId.Value);
    Assert.Empty(topology.Shafts);
    Assert.Empty(topology.ServicePoints);
    Assert.Equal(2, topology.EndpointBindings.Count);

    Assert.True(topology.TryResolveEndpoint(new EndpointId("inbound.main"), out var inbound));
    Assert.True(topology.TryResolveEndpoint(new EndpointId("outbound.main"), out var outbound));
    Assert.Equal("L1_LOAD_01", inbound.NodeId.Value);
    Assert.Equal("L2_UNLOAD_01", outbound.NodeId.Value);
    Assert.Empty(topology.GetOutgoingEdges(new NodeId("L2_UNLOAD_01")));
    Assert.Empty(topology.GetIncomingEdges(new NodeId("L2_UNLOAD_01")));
    Assert.False(topology.TryGetShaft(new ShaftId("LIFT_A"), out _));
    Assert.False(topology.TryGetShaftStopByTransferPoint(new NodeId("L1_TP_LIFT_A"), out _));
  }

  [Fact]
  public void CompilerRejectsTopologyWhenEndpointKindDoesNotMatchResolvedTarget()
  {
    var config = _loader.Load(
        """
        topologyId: INVALID-ENDPOINT-KIND
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: UNLOAD_NODE
            nodeType: StationNode
            levelId: L1
            stationId: UNLOAD_01
        stations:
          - stationId: UNLOAD_01
            stationType: UNLOAD
            controlMode: PASSIVE
            attachedNodeId: UNLOAD_NODE
            bufferCapacity: 1
        endpointMappings:
          - endpointId: inbound.main
            endpointKind: LOAD_STATION
            stationId: UNLOAD_01
        """);

    var exception = Assert.Throws<TopologyValidationException>(() => _compiler.Compile(config));

    Assert.Contains(exception.Errors, static error => error.Code == TopologyValidationErrorCode.InvalidEndpointTargetType);
  }

  [Fact]
  public void UnknownEndpointCanBeHandledViaTryResolveOrExplicitException()
  {
    var topology = CompileFixture("warehouse-a.nominal.yaml");
    var missingEndpoint = new EndpointId("missing.endpoint");

    Assert.False(topology.TryResolveEndpoint(missingEndpoint, out _));

    var exception = Assert.Throws<KeyNotFoundException>(() => topology.ResolveEndpoint(missingEndpoint));
    Assert.Contains("missing.endpoint", exception.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void CompilerSupportsMultipleBusinessAliasesForSameStationBoundary()
  {
    var config = _loader.Load(
        """
        topologyId: ALIAS-STATION
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: LOAD_NODE
            nodeType: StationNode
            levelId: L1
            stationId: LOAD_01
        stations:
          - stationId: LOAD_01
            stationType: LOAD
            controlMode: PASSIVE
            attachedNodeId: LOAD_NODE
            bufferCapacity: 2
        endpointMappings:
          - endpointId: inbound.main
            endpointKind: LOAD_STATION
            stationId: LOAD_01
          - endpointId: inbound.backup
            endpointKind: LOAD_STATION
            stationId: LOAD_01
        """);

    var topology = _compiler.Compile(config);
    var main = topology.ResolveEndpoint(new EndpointId("inbound.main"));
    var backup = topology.ResolveEndpoint(new EndpointId("inbound.backup"));

    Assert.Same(main.StationBoundary, backup.StationBoundary);
    Assert.Equal(
        ["inbound.backup", "inbound.main"],
        main.StationBoundary!.EndpointIds.Select(static endpointId => endpointId.Value).OrderBy(static value => value, StringComparer.Ordinal).ToArray());
  }

  [Fact]
  public void ServiceRegistrationAddsReusableTopologyServices()
  {
    var services = new ServiceCollection();
    services.AddWarehouseTopologyServices();

    using var provider = services.BuildServiceProvider();

    var loader = provider.GetRequiredService<IWarehouseTopologyConfigLoader>();
    var validator = provider.GetRequiredService<IWarehouseTopologyConfigValidator>();
    var compiler = provider.GetRequiredService<IWarehouseTopologyCompiler>();

    Assert.IsType<YamlWarehouseTopologyConfigLoader>(loader);
    Assert.IsType<WarehouseTopologyConfigValidator>(validator);
    Assert.IsType<WarehouseTopologyCompiler>(compiler);
    Assert.Same(loader, provider.GetRequiredService<IWarehouseTopologyConfigLoader>());
    Assert.Same(validator, provider.GetRequiredService<IWarehouseTopologyConfigValidator>());
    Assert.Same(compiler, provider.GetRequiredService<IWarehouseTopologyCompiler>());

    var topology = compiler.Compile(loader.LoadFromFile(GetTopologyFixturePath("warehouse-a.nominal.yaml")));
    Assert.Equal("WH-A", topology.TopologyId.Value);
  }

  private CompiledWarehouseTopology CompileFixture(string fileName) =>
      _compiler.Compile(_loader.LoadFromFile(GetTopologyFixturePath(fileName)));

  private static string GetTopologyFixturePath(string fileName) =>
      Path.Combine(TestRepositoryRoot.Get(), "topologies", "phase1", fileName);
}
