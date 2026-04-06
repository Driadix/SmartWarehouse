using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Domain.Stations;
using SmartWarehouse.PlatformCore.Domain.Topology;

namespace SmartWarehouse.PlatformCore.Application.Topology;

public sealed class CompiledWarehouseTopology
{
  private readonly Dictionary<NodeId, CompiledTopologyNode> _nodesById;
  private readonly Dictionary<EdgeId, CompiledTopologyEdge> _edgesById;
  private readonly Dictionary<ShaftId, CompiledCarrierShaft> _shaftsById;
  private readonly Dictionary<StationId, CompiledStationBoundary> _stationsById;
  private readonly Dictionary<ServicePointId, CompiledServicePoint> _servicePointsById;
  private readonly Dictionary<DeviceId, CompiledDeviceBinding> _deviceBindingsById;
  private readonly Dictionary<EndpointId, CompiledEndpointBinding> _endpointsById;
  private readonly Dictionary<NodeId, CompiledStationBoundary> _stationsByAttachedNodeId;
  private readonly Dictionary<NodeId, CompiledServicePoint> _servicePointsByNodeId;
  private readonly Dictionary<NodeId, CompiledCarrierShaftStop> _shaftStopsByTransferPointId;
  private readonly Dictionary<NodeId, CompiledCarrierShaftStop> _shaftStopsByCarrierNodeId;
  private readonly Dictionary<NodeId, IReadOnlyList<CompiledTopologyEdge>> _outgoingEdgesByNodeId;
  private readonly Dictionary<NodeId, IReadOnlyList<CompiledTopologyEdge>> _incomingEdgesByNodeId;

  internal CompiledWarehouseTopology(
      TopologyId topologyId,
      int version,
      IEnumerable<CompiledTopologyLevel>? levels,
      IEnumerable<CompiledTopologyNode>? nodes,
      IEnumerable<CompiledTopologyEdge>? edges,
      IEnumerable<CompiledCarrierShaft>? shafts,
      IEnumerable<CompiledStationBoundary>? stations,
      IEnumerable<CompiledServicePoint>? servicePoints,
      IEnumerable<CompiledDeviceBinding>? deviceBindings,
      IEnumerable<CompiledEndpointBinding>? endpoints)
  {
    TopologyId = topologyId;
    Version = TopologyConfigurationGuard.NonNegative(version, nameof(version));
    Levels = TopologyConfigurationGuard.UniqueReadOnlyList(levels, static level => level.LevelId, nameof(levels));
    Nodes = TopologyConfigurationGuard.UniqueReadOnlyList(nodes, static node => node.NodeId, nameof(nodes));
    Edges = TopologyConfigurationGuard.UniqueReadOnlyList(edges, static edge => edge.EdgeId, nameof(edges));
    Shafts = TopologyConfigurationGuard.UniqueReadOnlyList(shafts, static shaft => shaft.ShaftId, nameof(shafts));
    Stations = TopologyConfigurationGuard.UniqueReadOnlyList(stations, static station => station.StationId, nameof(stations));
    ServicePoints = TopologyConfigurationGuard.UniqueReadOnlyList(servicePoints, static servicePoint => servicePoint.ServicePointId, nameof(servicePoints));
    DeviceBindings = TopologyConfigurationGuard.UniqueReadOnlyList(deviceBindings, static deviceBinding => deviceBinding.DeviceId, nameof(deviceBindings));
    EndpointBindings = TopologyConfigurationGuard.UniqueReadOnlyList(endpoints, static endpoint => endpoint.EndpointId, nameof(endpoints));

    _nodesById = Nodes.ToDictionary(static node => node.NodeId);
    _edgesById = Edges.ToDictionary(static edge => edge.EdgeId);
    _shaftsById = Shafts.ToDictionary(static shaft => shaft.ShaftId);
    _stationsById = Stations.ToDictionary(static station => station.StationId);
    _servicePointsById = ServicePoints.ToDictionary(static servicePoint => servicePoint.ServicePointId);
    _deviceBindingsById = DeviceBindings.ToDictionary(static deviceBinding => deviceBinding.DeviceId);
    _endpointsById = EndpointBindings.ToDictionary(static endpoint => endpoint.EndpointId);
    _stationsByAttachedNodeId = Stations.ToDictionary(static station => station.AttachedNodeId);
    _servicePointsByNodeId = ServicePoints.ToDictionary(static servicePoint => servicePoint.NodeId);
    _shaftStopsByTransferPointId = Shafts
        .SelectMany(static shaft => shaft.Stops)
        .ToDictionary(static stop => stop.TransferPointId);
    _shaftStopsByCarrierNodeId = Shafts
        .SelectMany(static shaft => shaft.Stops)
        .ToDictionary(static stop => stop.CarrierNodeId);
    _outgoingEdgesByNodeId = BuildEdgeLookup(Edges, static edge => edge.FromNodeId);
    _incomingEdgesByNodeId = BuildEdgeLookup(Edges, static edge => edge.ToNodeId);
  }

  public TopologyId TopologyId { get; }

  public int Version { get; }

  public IReadOnlyList<CompiledTopologyLevel> Levels { get; }

  public IReadOnlyList<CompiledTopologyNode> Nodes { get; }

  public IReadOnlyList<CompiledTopologyEdge> Edges { get; }

  public IReadOnlyList<CompiledCarrierShaft> Shafts { get; }

  public IReadOnlyList<CompiledStationBoundary> Stations { get; }

  public IReadOnlyList<CompiledServicePoint> ServicePoints { get; }

  public IReadOnlyList<CompiledDeviceBinding> DeviceBindings { get; }

  public IReadOnlyList<CompiledEndpointBinding> EndpointBindings { get; }

  public bool TryGetNode(NodeId nodeId, out CompiledTopologyNode node) =>
      _nodesById.TryGetValue(nodeId, out node!);

  public bool TryGetEdge(EdgeId edgeId, out CompiledTopologyEdge edge) =>
      _edgesById.TryGetValue(edgeId, out edge!);

  public bool TryGetShaft(ShaftId shaftId, out CompiledCarrierShaft shaft) =>
      _shaftsById.TryGetValue(shaftId, out shaft!);

  public bool TryGetStation(StationId stationId, out CompiledStationBoundary station) =>
      _stationsById.TryGetValue(stationId, out station!);

  public bool TryGetStationByAttachedNode(NodeId nodeId, out CompiledStationBoundary station) =>
      _stationsByAttachedNodeId.TryGetValue(nodeId, out station!);

  public bool TryGetServicePoint(ServicePointId servicePointId, out CompiledServicePoint servicePoint) =>
      _servicePointsById.TryGetValue(servicePointId, out servicePoint!);

  public bool TryGetServicePointByNode(NodeId nodeId, out CompiledServicePoint servicePoint) =>
      _servicePointsByNodeId.TryGetValue(nodeId, out servicePoint!);

  public bool TryGetDeviceBinding(DeviceId deviceId, out CompiledDeviceBinding binding) =>
      _deviceBindingsById.TryGetValue(deviceId, out binding!);

  public bool TryResolveEndpoint(EndpointId endpointId, out CompiledEndpointBinding endpoint) =>
      _endpointsById.TryGetValue(endpointId, out endpoint!);

  public CompiledEndpointBinding ResolveEndpoint(EndpointId endpointId)
  {
    if (TryResolveEndpoint(endpointId, out var endpoint))
    {
      return endpoint;
    }

    throw new KeyNotFoundException($"Endpoint '{endpointId}' is not present in compiled topology '{TopologyId}'.");
  }

  public bool TryGetShaftStopByTransferPoint(NodeId transferPointId, out CompiledCarrierShaftStop stop) =>
      _shaftStopsByTransferPointId.TryGetValue(transferPointId, out stop!);

  public bool TryGetShaftStopByCarrierNode(NodeId carrierNodeId, out CompiledCarrierShaftStop stop) =>
      _shaftStopsByCarrierNodeId.TryGetValue(carrierNodeId, out stop!);

  public IReadOnlyList<CompiledTopologyEdge> GetOutgoingEdges(NodeId nodeId) =>
      _outgoingEdgesByNodeId.TryGetValue(nodeId, out var edges)
          ? edges
          : Array.Empty<CompiledTopologyEdge>();

  public IReadOnlyList<CompiledTopologyEdge> GetIncomingEdges(NodeId nodeId) =>
      _incomingEdgesByNodeId.TryGetValue(nodeId, out var edges)
          ? edges
          : Array.Empty<CompiledTopologyEdge>();

  private static Dictionary<NodeId, IReadOnlyList<CompiledTopologyEdge>> BuildEdgeLookup(
      IEnumerable<CompiledTopologyEdge> edges,
      Func<CompiledTopologyEdge, NodeId> nodeSelector)
  {
    return edges
        .GroupBy(nodeSelector)
        .ToDictionary(
            static group => group.Key,
            static group => (IReadOnlyList<CompiledTopologyEdge>)Array.AsReadOnly(group.ToArray()));
  }
}

public sealed class CompiledTopologyLevel
{
  public CompiledTopologyLevel(LevelId levelId, int ordinal, string? name = null)
  {
    LevelId = levelId;
    Ordinal = TopologyConfigurationGuard.NonNegative(ordinal, nameof(ordinal));
    Name = TopologyConfigurationGuard.OptionalNotWhiteSpace(name, nameof(name));
  }

  public LevelId LevelId { get; }

  public int Ordinal { get; }

  public string? Name { get; }
}

public sealed class CompiledTopologyNode
{
  public CompiledTopologyNode(
      NodeId nodeId,
      NodeType nodeType,
      LevelId? levelId,
      int? levelOrdinal,
      IEnumerable<string>? tags,
      StationId? stationId = null,
      ShaftId? shaftId = null,
      ServicePointId? servicePointId = null)
  {
    if (levelOrdinal is < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(levelOrdinal), levelOrdinal, "Level ordinal cannot be negative.");
    }

    LevelId = levelId;
    LevelOrdinal = levelOrdinal;
    Tags = TopologyConfigurationGuard.UniqueReadOnlyList(tags, static tag => tag, nameof(tags));
    StationId = stationId;
    ShaftId = shaftId;
    ServicePointId = servicePointId;
    RuntimeNode = new Node(nodeId, nodeType, levelOrdinal);

    switch (nodeType)
    {
      case NodeType.TravelNode:
      case NodeType.SwitchNode:
        if (stationId is not null || shaftId is not null || servicePointId is not null)
        {
          throw new ArgumentException($"{nodeType} cannot reference station, shaft, or service point identifiers.");
        }

        break;
      case NodeType.TransferPoint:
      case NodeType.CarrierNode:
        if (shaftId is null)
        {
          throw new ArgumentException($"{nodeType} requires a shaft identifier.", nameof(shaftId));
        }

        if (stationId is not null || servicePointId is not null)
        {
          throw new ArgumentException($"{nodeType} cannot reference a station or service point.");
        }

        break;
      case NodeType.StationNode:
        if (stationId is null)
        {
          throw new ArgumentException("StationNode requires a station identifier.", nameof(stationId));
        }

        if (shaftId is not null || servicePointId is not null)
        {
          throw new ArgumentException("StationNode cannot reference a shaft or service point.");
        }

        break;
      case NodeType.ChargeNode:
      case NodeType.ServiceNode:
        if (servicePointId is null)
        {
          throw new ArgumentException($"{nodeType} requires a service point identifier.", nameof(servicePointId));
        }

        if (stationId is not null || shaftId is not null)
        {
          throw new ArgumentException($"{nodeType} cannot reference a station or shaft.");
        }

        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, "Unsupported node type.");
    }
  }

  public Node RuntimeNode { get; }

  public NodeId NodeId => RuntimeNode.NodeId;

  public NodeType NodeType => RuntimeNode.NodeType;

  public LevelId? LevelId { get; }

  public int? LevelOrdinal { get; }

  public IReadOnlyList<string> Tags { get; }

  public StationId? StationId { get; }

  public ShaftId? ShaftId { get; }

  public ServicePointId? ServicePointId { get; }
}

public sealed class CompiledTopologyEdge
{
  public CompiledTopologyEdge(
      EdgeId edgeId,
      CompiledTopologyNode fromNode,
      CompiledTopologyNode toNode,
      EdgeTraversalMode traversalMode,
      decimal weight)
  {
    FromNode = fromNode ?? throw new ArgumentNullException(nameof(fromNode));
    ToNode = toNode ?? throw new ArgumentNullException(nameof(toNode));
    RuntimeEdge = new Edge(edgeId, fromNode.NodeId, toNode.NodeId, traversalMode, weight);
  }

  public Edge RuntimeEdge { get; }

  public EdgeId EdgeId => RuntimeEdge.EdgeId;

  public NodeId FromNodeId => RuntimeEdge.FromNode;

  public NodeId ToNodeId => RuntimeEdge.ToNode;

  public EdgeTraversalMode TraversalMode => RuntimeEdge.TraversalMode;

  public decimal Weight => RuntimeEdge.Weight;

  public CompiledTopologyNode FromNode { get; }

  public CompiledTopologyNode ToNode { get; }
}

public sealed class CompiledCarrierShaft
{
  private readonly Dictionary<LevelId, CompiledCarrierShaftStop> _stopsByLevelId;
  private readonly Dictionary<NodeId, CompiledCarrierShaftStop> _stopsByTransferPointId;
  private readonly Dictionary<NodeId, CompiledCarrierShaftStop> _stopsByCarrierNodeId;

  public CompiledCarrierShaft(
      ShaftId shaftId,
      DeviceId carrierDeviceId,
      int slotCount,
      IEnumerable<CompiledCarrierShaftStop>? stops)
  {
    ShaftId = shaftId;
    CarrierDeviceId = carrierDeviceId;
    SlotCount = TopologyConfigurationGuard.Positive(slotCount, nameof(slotCount));
    Stops = TopologyConfigurationGuard.UniqueReadOnlyList(stops, static stop => stop.LevelId, nameof(stops), allowEmpty: false);

    _stopsByLevelId = Stops.ToDictionary(static stop => stop.LevelId);
    _stopsByTransferPointId = Stops.ToDictionary(static stop => stop.TransferPointId);
    _stopsByCarrierNodeId = Stops.ToDictionary(static stop => stop.CarrierNodeId);
  }

  public ShaftId ShaftId { get; }

  public DeviceId CarrierDeviceId { get; }

  public int SlotCount { get; }

  public IReadOnlyList<CompiledCarrierShaftStop> Stops { get; }

  public bool TryGetStop(LevelId levelId, out CompiledCarrierShaftStop stop) =>
      _stopsByLevelId.TryGetValue(levelId, out stop!);

  public bool TryGetStopByTransferPoint(NodeId transferPointId, out CompiledCarrierShaftStop stop) =>
      _stopsByTransferPointId.TryGetValue(transferPointId, out stop!);

  public bool TryGetStopByCarrierNode(NodeId carrierNodeId, out CompiledCarrierShaftStop stop) =>
      _stopsByCarrierNodeId.TryGetValue(carrierNodeId, out stop!);
}

public sealed class CompiledCarrierShaftStop
{
  public CompiledCarrierShaftStop(
      ShaftId shaftId,
      DeviceId carrierDeviceId,
      LevelId levelId,
      int levelOrdinal,
      CompiledTopologyNode carrierNode,
      CompiledTopologyNode transferPoint)
  {
    if (levelOrdinal < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(levelOrdinal), levelOrdinal, "Level ordinal cannot be negative.");
    }

    CarrierNode = carrierNode ?? throw new ArgumentNullException(nameof(carrierNode));
    TransferPoint = transferPoint ?? throw new ArgumentNullException(nameof(transferPoint));

    if (carrierNode.NodeType != NodeType.CarrierNode || carrierNode.ShaftId != shaftId || carrierNode.LevelId != levelId)
    {
      throw new ArgumentException("Carrier shaft stop must reference a CarrierNode on the same shaft and level.", nameof(carrierNode));
    }

    if (transferPoint.NodeType != NodeType.TransferPoint || transferPoint.ShaftId != shaftId || transferPoint.LevelId != levelId)
    {
      throw new ArgumentException("Carrier shaft stop must reference a TransferPoint on the same shaft and level.", nameof(transferPoint));
    }

    ShaftId = shaftId;
    CarrierDeviceId = carrierDeviceId;
    LevelId = levelId;
    LevelOrdinal = levelOrdinal;
  }

  public ShaftId ShaftId { get; }

  public DeviceId CarrierDeviceId { get; }

  public LevelId LevelId { get; }

  public int LevelOrdinal { get; }

  public CompiledTopologyNode CarrierNode { get; }

  public NodeId CarrierNodeId => CarrierNode.NodeId;

  public CompiledTopologyNode TransferPoint { get; }

  public NodeId TransferPointId => TransferPoint.NodeId;
}

public sealed class CompiledStationBoundary
{
  public CompiledStationBoundary(
      StationId stationId,
      StationType stationType,
      CompiledTopologyNode attachedNode,
      int bufferCapacity,
      IEnumerable<EndpointId>? endpointIds)
  {
    if (stationType is not (StationType.Load or StationType.Unload))
    {
      throw new ArgumentOutOfRangeException(nameof(stationType), stationType, "Unsupported station type.");
    }

    AttachedNode = attachedNode ?? throw new ArgumentNullException(nameof(attachedNode));

    if (attachedNode.NodeType != NodeType.StationNode || attachedNode.StationId != stationId)
    {
      throw new ArgumentException("Station boundary must target a matching StationNode.", nameof(attachedNode));
    }

    StationId = stationId;
    StationType = stationType;
    BufferCapacity = TopologyConfigurationGuard.NonNegative(bufferCapacity, nameof(bufferCapacity));
    EndpointIds = TopologyConfigurationGuard.UniqueReadOnlyList(endpointIds, static endpointId => endpointId, nameof(endpointIds));
  }

  public StationId StationId { get; }

  public StationType StationType { get; }

  public StationControlMode ControlMode { get; } = StationControlMode.Passive;

  public CompiledTopologyNode AttachedNode { get; }

  public NodeId AttachedNodeId => AttachedNode.NodeId;

  public LevelId? LevelId => AttachedNode.LevelId;

  public int? LevelOrdinal => AttachedNode.LevelOrdinal;

  public int BufferCapacity { get; }

  public IReadOnlyList<EndpointId> EndpointIds { get; }

  public ExecutionResourceRef ExecutionResourceRef => ExecutionResourceRef.ForStationBoundary(StationId);

  public PayloadCustodyHolder PayloadCustodyHolder => PayloadCustodyHolder.ForStationBoundary(StationId);

  public FaultSourceRef FaultSourceRef => FaultSourceRef.ForStationBoundary(StationId);

  public StationBoundary CreateBoundary(StationReadiness readiness) =>
      StationType switch
      {
        StationType.Load => new LoadStation(StationId, AttachedNodeId, readiness, BufferCapacity),
        StationType.Unload => new UnloadStation(StationId, AttachedNodeId, readiness, BufferCapacity),
        _ => throw new InvalidOperationException($"Unsupported station type '{StationType}'.")
      };
}

public sealed class CompiledServicePoint
{
  public CompiledServicePoint(
      ServicePointId servicePointId,
      ServicePointType servicePointType,
      CompiledTopologyNode node,
      ServicePointPassiveSemantics passiveSemantics,
      IEnumerable<EndpointId>? endpointIds)
  {
    Node = node ?? throw new ArgumentNullException(nameof(node));
    ServicePointId = servicePointId;
    ServicePointType = servicePointType;
    PassiveSemantics = passiveSemantics;
    EndpointIds = TopologyConfigurationGuard.UniqueReadOnlyList(endpointIds, static endpointId => endpointId, nameof(endpointIds));

    var expectedNodeType = servicePointType switch
    {
      ServicePointType.Charge => NodeType.ChargeNode,
      ServicePointType.Service => NodeType.ServiceNode,
      _ => throw new ArgumentOutOfRangeException(nameof(servicePointType), servicePointType, "Unsupported service point type.")
    };

    if (node.NodeType != expectedNodeType || node.ServicePointId != servicePointId)
    {
      throw new ArgumentException($"Service point must target a matching {expectedNodeType}.", nameof(node));
    }
  }

  public ServicePointId ServicePointId { get; }

  public ServicePointType ServicePointType { get; }

  public CompiledTopologyNode Node { get; }

  public NodeId NodeId => Node.NodeId;

  public LevelId? LevelId => Node.LevelId;

  public int? LevelOrdinal => Node.LevelOrdinal;

  public ServicePointPassiveSemantics PassiveSemantics { get; }

  public IReadOnlyList<EndpointId> EndpointIds { get; }
}

public sealed class CompiledDeviceBinding
{
  public CompiledDeviceBinding(
      DeviceId deviceId,
      DeviceFamily family,
      NodeId? initialNodeId = null,
      NodeId? homeNodeId = null,
      ShaftId? shaftId = null)
  {
    DeviceId = deviceId;
    Family = family;
    InitialNodeId = initialNodeId;
    HomeNodeId = homeNodeId;
    ShaftId = shaftId;

    switch (family)
    {
      case DeviceFamily.Shuttle3D:
        if (shaftId is not null)
        {
          throw new ArgumentException("Shuttle3D binding cannot reference a shaft.", nameof(shaftId));
        }

        break;
      case DeviceFamily.HybridLift:
        if (shaftId is null)
        {
          throw new ArgumentException("HybridLift binding requires a shaft identifier.", nameof(shaftId));
        }

        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(family), family, "Unsupported device family.");
    }
  }

  public DeviceId DeviceId { get; }

  public DeviceFamily Family { get; }

  public NodeId? InitialNodeId { get; }

  public NodeId? HomeNodeId { get; }

  public ShaftId? ShaftId { get; }

  public ExecutionResourceRef ExecutionResourceRef => ExecutionResourceRef.ForDevice(DeviceId);
}

public sealed class CompiledEndpointBinding
{
  public CompiledEndpointBinding(
      EndpointId endpointId,
      EndpointKind endpointKind,
      CompiledStationBoundary? stationBoundary = null,
      CompiledServicePoint? servicePoint = null)
  {
    EndpointId = endpointId;
    EndpointKind = endpointKind;
    StationBoundary = stationBoundary;
    ServicePoint = servicePoint;

    switch (endpointKind)
    {
      case EndpointKind.LoadStation:
        if (stationBoundary is null || stationBoundary.StationType != StationType.Load || servicePoint is not null)
        {
          throw new ArgumentException("LoadStation endpoint must reference a load station boundary.", nameof(stationBoundary));
        }

        Node = stationBoundary.AttachedNode;
        break;
      case EndpointKind.UnloadStation:
        if (stationBoundary is null || stationBoundary.StationType != StationType.Unload || servicePoint is not null)
        {
          throw new ArgumentException("UnloadStation endpoint must reference an unload station boundary.", nameof(stationBoundary));
        }

        Node = stationBoundary.AttachedNode;
        break;
      case EndpointKind.ChargePoint:
        if (servicePoint is null || servicePoint.ServicePointType != ServicePointType.Charge || stationBoundary is not null)
        {
          throw new ArgumentException("ChargePoint endpoint must reference a charge service point.", nameof(servicePoint));
        }

        Node = servicePoint.Node;
        break;
      case EndpointKind.ServicePoint:
        if (servicePoint is null || servicePoint.ServicePointType != ServicePointType.Service || stationBoundary is not null)
        {
          throw new ArgumentException("ServicePoint endpoint must reference a service service point.", nameof(servicePoint));
        }

        Node = servicePoint.Node;
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(endpointKind), endpointKind, "Unsupported endpoint kind.");
    }
  }

  public EndpointId EndpointId { get; }

  public EndpointKind EndpointKind { get; }

  public CompiledTopologyNode Node { get; }

  public NodeId NodeId => Node.NodeId;

  public LevelId? LevelId => Node.LevelId;

  public int? LevelOrdinal => Node.LevelOrdinal;

  public CompiledStationBoundary? StationBoundary { get; }

  public StationId? StationId => StationBoundary?.StationId;

  public CompiledServicePoint? ServicePoint { get; }

  public ServicePointId? ServicePointId => ServicePoint?.ServicePointId;

  public ExecutionResourceRef? BoundaryExecutionResourceRef => StationBoundary?.ExecutionResourceRef;

  public PayloadCustodyHolder? BoundaryPayloadCustodyHolder => StationBoundary?.PayloadCustodyHolder;
}
