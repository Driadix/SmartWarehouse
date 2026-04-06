using SmartWarehouse.PlatformCore.Application.Topology;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class TopologyConfigurationValidatorTests
{
  private readonly YamlWarehouseTopologyConfigLoader _loader = new();
  private readonly WarehouseTopologyConfigValidator _validator = new();

  [Fact]
  public void NominalFixturePassesStaticTopologyValidation()
  {
    var config = _loader.LoadFromFile(GetTopologyFixturePath("warehouse-a.nominal.yaml"));
    var errors = _validator.Validate(config);

    Assert.Empty(errors);
  }

  [Fact]
  public void NoRouteFixtureRemainsStructurallyValidForStaticTopologyValidation()
  {
    var config = _loader.LoadFromFile(GetTopologyFixturePath("warehouse-a.no-route.yaml"));
    var errors = _validator.Validate(config);

    Assert.Empty(errors);
  }

  [Fact]
  public void ValidatorRejectsStationAttachedToNonStationNode()
  {
    var errors = Validate(
        """
        topologyId: INVALID-STATION
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: TRAVEL_1
            nodeType: TravelNode
            levelId: L1
        stations:
          - stationId: LOAD_01
            stationType: LOAD
            controlMode: PASSIVE
            attachedNodeId: TRAVEL_1
            bufferCapacity: 1
        """);

    Assert.Contains(errors, static error => error.Code == TopologyValidationErrorCode.InvalidStationAttachment);
  }

  [Fact]
  public void ValidatorRejectsServicePointBoundToWrongNodeType()
  {
    var errors = Validate(
        """
        topologyId: INVALID-SERVICE-POINT
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: NODE_1
            nodeType: TravelNode
            levelId: L1
        servicePoints:
          - servicePointId: CHARGE_01
            servicePointType: CHARGE
            nodeId: NODE_1
            passiveSemantics: ARRIVAL_CONFIRMS_ENGAGEMENT
        """);

    Assert.Contains(errors, static error => error.Code == TopologyValidationErrorCode.InvalidServicePointNode);
  }

  [Fact]
  public void ValidatorRejectsCarrierOnlyEdgeOutsideCarrierShaft()
  {
    var errors = Validate(
        """
        topologyId: INVALID-CARRIER-EDGE
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: A
            nodeType: TravelNode
            levelId: L1
          - nodeId: B
            nodeType: TravelNode
            levelId: L1
        edges:
          - edgeId: E1
            fromNodeId: A
            toNodeId: B
            traversalMode: CARRIER_ONLY
            weight: 1
        """);

    Assert.Contains(errors, static error => error.Code == TopologyValidationErrorCode.InvalidCarrierOnlyEdge);
  }

  [Fact]
  public void ValidatorRejectsOpenEdgeTouchingCarrierNode()
  {
    var errors = Validate(
        """
        topologyId: INVALID-CARRIER-TRANSITION
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: TRAVEL_1
            nodeType: TravelNode
            levelId: L1
          - nodeId: TP_1
            nodeType: TransferPoint
            levelId: L1
            shaftId: LIFT_A
          - nodeId: CARRIER_1
            nodeType: CarrierNode
            levelId: L1
            shaftId: LIFT_A
        edges:
          - edgeId: E1
            fromNodeId: TRAVEL_1
            toNodeId: CARRIER_1
            traversalMode: OPEN
            weight: 1
        shafts:
          - shaftId: LIFT_A
            carrierDeviceId: LIFT_A_DEVICE
            slotCount: 1
            stops:
              - levelId: L1
                carrierNodeId: CARRIER_1
                transferPointId: TP_1
        deviceBindings:
          - deviceId: LIFT_A_DEVICE
            family: HybridLift
            shaftId: LIFT_A
            initialNodeId: CARRIER_1
        """);

    Assert.Contains(errors, static error => error.Code == TopologyValidationErrorCode.InvalidCarrierNodeTraversalEdge);
  }

  [Fact]
  public void ValidatorRejectsDuplicateCarrierNodesOnSameShaftLevel()
  {
    var errors = Validate(
        """
        topologyId: DUPLICATE-CARRIER-LEVEL
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: TP_1
            nodeType: TransferPoint
            levelId: L1
            shaftId: LIFT_A
          - nodeId: CARRIER_1
            nodeType: CarrierNode
            levelId: L1
            shaftId: LIFT_A
          - nodeId: CARRIER_2
            nodeType: CarrierNode
            levelId: L1
            shaftId: LIFT_A
        shafts:
          - shaftId: LIFT_A
            carrierDeviceId: LIFT_A_DEVICE
            slotCount: 1
            stops:
              - levelId: L1
                carrierNodeId: CARRIER_1
                transferPointId: TP_1
        deviceBindings:
          - deviceId: LIFT_A_DEVICE
            family: HybridLift
            shaftId: LIFT_A
            initialNodeId: CARRIER_1
        """);

    Assert.Contains(errors, static error => error.Code == TopologyValidationErrorCode.DuplicateCarrierNodeLevel);
  }

  [Theory]
  [InlineData("OPEN")]
  [InlineData("RESTRICTED")]
  public void ValidatorRejectsNonCarrierEdgeCrossingLevels(string traversalModeLiteral)
  {
    var errors = Validate(
        $$"""
        topologyId: INVALID-CROSS-LEVEL-EDGE
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
          - levelId: L2
            ordinal: 2
        nodes:
          - nodeId: TRAVEL_L1
            nodeType: TravelNode
            levelId: L1
          - nodeId: TRAVEL_L2
            nodeType: TravelNode
            levelId: L2
        edges:
          - edgeId: E1
            fromNodeId: TRAVEL_L1
            toNodeId: TRAVEL_L2
            traversalMode: {{traversalModeLiteral}}
            weight: 1
        """);

    var error = Assert.Single(
        errors,
        static candidate => candidate.Code == TopologyValidationErrorCode.InvalidCrossLevelTraversalEdge);

    Assert.Contains("E1", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void ValidatorRejectsCarrierNodeWithoutMatchingTransferPoint()
  {
    var errors = Validate(
        """
        topologyId: MISSING-TRANSFER-POINT
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
          - levelId: L2
            ordinal: 2
        nodes:
          - nodeId: TP_2
            nodeType: TransferPoint
            levelId: L2
            shaftId: LIFT_A
          - nodeId: CARRIER_1
            nodeType: CarrierNode
            levelId: L1
            shaftId: LIFT_A
        shafts:
          - shaftId: LIFT_A
            carrierDeviceId: LIFT_A_DEVICE
            slotCount: 1
            stops:
              - levelId: L1
                carrierNodeId: CARRIER_1
                transferPointId: TP_2
        deviceBindings:
          - deviceId: LIFT_A_DEVICE
            family: HybridLift
            shaftId: LIFT_A
            initialNodeId: CARRIER_1
        """);

    Assert.Contains(errors, static error => error.Code == TopologyValidationErrorCode.MissingTransferPointForCarrierNode);
  }

  [Fact]
  public void ValidatorRejectsShaftBoundToWrongHybridLift()
  {
    var errors = Validate(
        """
        topologyId: INVALID-SHAFT-BINDING
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: TP_1
            nodeType: TransferPoint
            levelId: L1
            shaftId: LIFT_A
          - nodeId: CARRIER_1
            nodeType: CarrierNode
            levelId: L1
            shaftId: LIFT_A
        shafts:
          - shaftId: LIFT_A
            carrierDeviceId: LIFT_A_DEVICE
            slotCount: 1
            stops:
              - levelId: L1
                carrierNodeId: CARRIER_1
                transferPointId: TP_1
        deviceBindings:
          - deviceId: LIFT_B_DEVICE
            family: HybridLift
            shaftId: LIFT_A
            initialNodeId: CARRIER_1
        """);

    Assert.Contains(errors, static error => error.Code == TopologyValidationErrorCode.InvalidShaftCarrierDeviceBinding);
  }

  [Fact]
  public void ValidatorRejectsEndpointMappedToUnknownStation()
  {
    var errors = Validate(
        """
        topologyId: INVALID-ENDPOINT
        version: 1
        endpointMappings:
          - endpointId: inbound.main
            endpointKind: LOAD_STATION
            stationId: LOAD_01
        """);

    Assert.Contains(errors, static error => error.Code == TopologyValidationErrorCode.InvalidEndpointReference);
  }

  [Fact]
  public void ValidatorRejectsLoadEndpointMappedToUnloadStation()
  {
    var errors = Validate(
        """
        topologyId: INVALID-ENDPOINT-STATION-TYPE
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

    Assert.Contains(errors, static error => error.Code == TopologyValidationErrorCode.InvalidEndpointTargetType);
  }

  [Fact]
  public void ValidatorRejectsChargeEndpointMappedToServiceNode()
  {
    var errors = Validate(
        """
        topologyId: INVALID-ENDPOINT-SERVICE-TYPE
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: SERVICE_NODE
            nodeType: ServiceNode
            levelId: L1
            servicePointId: SERVICE_01
        servicePoints:
          - servicePointId: SERVICE_01
            servicePointType: SERVICE
            nodeId: SERVICE_NODE
            passiveSemantics: ARRIVAL_CONFIRMS_ENGAGEMENT
        endpointMappings:
          - endpointId: charge.main
            endpointKind: CHARGE_POINT
            servicePointId: SERVICE_01
        """);

    Assert.Contains(errors, static error => error.Code == TopologyValidationErrorCode.InvalidEndpointTargetType);
  }

  [Fact]
  public void ValidatorRejectsEndpointIdentifierThatReusesDeviceIdentifier()
  {
    var errors = Validate(
        """
        topologyId: ENDPOINT-CONFLICT
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
            bufferCapacity: 1
        deviceBindings:
          - deviceId: SHUTTLE_01
            family: Shuttle3D
            initialNodeId: LOAD_NODE
        endpointMappings:
          - endpointId: SHUTTLE_01
            endpointKind: LOAD_STATION
            stationId: LOAD_01
        """);

    Assert.Contains(errors, static error => error.Code == TopologyValidationErrorCode.EndpointIdConflictsWithDeviceId);
  }

  [Fact]
  public void EnsureValidAggregatesMultipleViolations()
  {
    var config = _loader.Load(
        """
        topologyId: MULTI-ERROR
        version: 1
        levels:
          - levelId: L1
            ordinal: 1
        nodes:
          - nodeId: NODE_1
            nodeType: TravelNode
            levelId: L1
          - nodeId: NODE_2
            nodeType: TravelNode
            levelId: L1
        edges:
          - edgeId: E1
            fromNodeId: NODE_1
            toNodeId: NODE_2
            traversalMode: CARRIER_ONLY
            weight: 1
        stations:
          - stationId: LOAD_01
            stationType: LOAD
            controlMode: PASSIVE
            attachedNodeId: NODE_1
            bufferCapacity: 1
        """);

    var exception = Assert.Throws<TopologyValidationException>(() => _validator.EnsureValid(config));

    Assert.True(exception.Errors.Count >= 2);
    Assert.Contains(exception.Errors, static error => error.Code == TopologyValidationErrorCode.InvalidStationAttachment);
    Assert.Contains(exception.Errors, static error => error.Code == TopologyValidationErrorCode.InvalidCarrierOnlyEdge);
  }

  private IReadOnlyList<TopologyValidationError> Validate(string yaml) =>
      _validator.Validate(_loader.Load(yaml));

  private static string GetTopologyFixturePath(string fileName) =>
      Path.Combine(TestRepositoryRoot.Get(), "topologies", "phase1", fileName);
}
