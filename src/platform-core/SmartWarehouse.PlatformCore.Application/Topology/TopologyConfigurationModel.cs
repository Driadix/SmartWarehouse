using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Application.Topology;

public sealed class WarehouseTopologyConfig
{
  public WarehouseTopologyConfig(
      TopologyId topologyId,
      int version,
      IEnumerable<LevelConfig>? levels,
      IEnumerable<TopologyNodeConfig>? nodes,
      IEnumerable<TopologyEdgeConfig>? edges,
      IEnumerable<CarrierShaftConfig>? shafts,
      IEnumerable<StationConfig>? stations,
      IEnumerable<ServicePointConfig>? servicePoints,
      IEnumerable<DeviceBindingConfig>? deviceBindings,
      IEnumerable<EndpointMappingConfig>? endpointMappings)
  {
    TopologyId = topologyId;
    Version = TopologyConfigurationGuard.NonNegative(version, nameof(version));
    Levels = TopologyConfigurationGuard.UniqueReadOnlyList(levels, static item => item.LevelId, nameof(levels));
    Nodes = TopologyConfigurationGuard.UniqueReadOnlyList(nodes, static item => item.NodeId, nameof(nodes));
    Edges = TopologyConfigurationGuard.UniqueReadOnlyList(edges, static item => item.EdgeId, nameof(edges));
    Shafts = TopologyConfigurationGuard.UniqueReadOnlyList(shafts, static item => item.ShaftId, nameof(shafts));
    Stations = TopologyConfigurationGuard.UniqueReadOnlyList(stations, static item => item.StationId, nameof(stations));
    ServicePoints = TopologyConfigurationGuard.UniqueReadOnlyList(servicePoints, static item => item.ServicePointId, nameof(servicePoints));
    DeviceBindings = TopologyConfigurationGuard.UniqueReadOnlyList(deviceBindings, static item => item.DeviceId, nameof(deviceBindings));
    EndpointMappings = TopologyConfigurationGuard.UniqueReadOnlyList(endpointMappings, static item => item.EndpointId, nameof(endpointMappings));
  }

  public TopologyId TopologyId { get; }

  public int Version { get; }

  public IReadOnlyList<LevelConfig> Levels { get; }

  public IReadOnlyList<TopologyNodeConfig> Nodes { get; }

  public IReadOnlyList<TopologyEdgeConfig> Edges { get; }

  public IReadOnlyList<CarrierShaftConfig> Shafts { get; }

  public IReadOnlyList<StationConfig> Stations { get; }

  public IReadOnlyList<ServicePointConfig> ServicePoints { get; }

  public IReadOnlyList<DeviceBindingConfig> DeviceBindings { get; }

  public IReadOnlyList<EndpointMappingConfig> EndpointMappings { get; }
}

public sealed class LevelConfig
{
  public LevelConfig(LevelId levelId, int ordinal, string? name = null)
  {
    LevelId = levelId;
    Ordinal = TopologyConfigurationGuard.NonNegative(ordinal, nameof(ordinal));
    Name = TopologyConfigurationGuard.OptionalNotWhiteSpace(name, nameof(name));
  }

  public LevelId LevelId { get; }

  public int Ordinal { get; }

  public string? Name { get; }
}

public sealed class TopologyNodeConfig
{
  public TopologyNodeConfig(
      NodeId nodeId,
      NodeType nodeType,
      LevelId? levelId,
      IEnumerable<string>? tags,
      StationId? stationId = null,
      ShaftId? shaftId = null,
      ServicePointId? servicePointId = null)
  {
    NodeId = nodeId;
    NodeType = nodeType;
    LevelId = levelId;
    Tags = TopologyConfigurationGuard.UniqueReadOnlyList(
        (tags ?? Enumerable.Empty<string>())
        .Select(tag => TopologyConfigurationGuard.NotWhiteSpace(tag, nameof(tags))),
        static tag => tag,
        nameof(tags));
    StationId = stationId;
    ShaftId = shaftId;
    ServicePointId = servicePointId;

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

  public NodeId NodeId { get; }

  public NodeType NodeType { get; }

  public LevelId? LevelId { get; }

  public IReadOnlyList<string> Tags { get; }

  public StationId? StationId { get; }

  public ShaftId? ShaftId { get; }

  public ServicePointId? ServicePointId { get; }
}

public sealed class TopologyEdgeConfig
{
  public TopologyEdgeConfig(
      EdgeId edgeId,
      NodeId fromNodeId,
      NodeId toNodeId,
      EdgeTraversalMode traversalMode,
      decimal weight)
  {
    if (fromNodeId == toNodeId)
    {
      throw new ArgumentException("Edge must connect two different nodes.", nameof(toNodeId));
    }

    EdgeId = edgeId;
    FromNodeId = fromNodeId;
    ToNodeId = toNodeId;
    TraversalMode = traversalMode;
    Weight = TopologyConfigurationGuard.Positive(weight, nameof(weight));
  }

  public EdgeId EdgeId { get; }

  public NodeId FromNodeId { get; }

  public NodeId ToNodeId { get; }

  public EdgeTraversalMode TraversalMode { get; }

  public decimal Weight { get; }
}

public sealed class CarrierShaftConfig
{
  public CarrierShaftConfig(
      ShaftId shaftId,
      DeviceId carrierDeviceId,
      int slotCount,
      IEnumerable<CarrierShaftStopConfig>? stops)
  {
    ShaftId = shaftId;
    CarrierDeviceId = carrierDeviceId;
    SlotCount = TopologyConfigurationGuard.Positive(slotCount, nameof(slotCount));

    if (SlotCount != 1)
    {
      throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "Phase 1 supports only slotCount = 1.");
    }

    Stops = TopologyConfigurationGuard.UniqueReadOnlyList(stops, static item => item.LevelId, nameof(stops), allowEmpty: false);
  }

  public ShaftId ShaftId { get; }

  public DeviceId CarrierDeviceId { get; }

  public int SlotCount { get; }

  public IReadOnlyList<CarrierShaftStopConfig> Stops { get; }
}

public sealed class CarrierShaftStopConfig
{
  public CarrierShaftStopConfig(LevelId levelId, NodeId carrierNodeId, NodeId transferPointId)
  {
    LevelId = levelId;
    CarrierNodeId = carrierNodeId;
    TransferPointId = transferPointId;
  }

  public LevelId LevelId { get; }

  public NodeId CarrierNodeId { get; }

  public NodeId TransferPointId { get; }
}

public sealed class StationConfig
{
  public StationConfig(
      StationId stationId,
      StationType stationType,
      StationControlMode controlMode,
      NodeId attachedNodeId,
      int bufferCapacity)
  {
    if (controlMode != StationControlMode.Passive)
    {
      throw new ArgumentOutOfRangeException(nameof(controlMode), controlMode, "Phase 1 supports only passive stations.");
    }

    StationId = stationId;
    StationType = stationType;
    ControlMode = controlMode;
    AttachedNodeId = attachedNodeId;
    BufferCapacity = TopologyConfigurationGuard.NonNegative(bufferCapacity, nameof(bufferCapacity));
  }

  public StationId StationId { get; }

  public StationType StationType { get; }

  public StationControlMode ControlMode { get; }

  public NodeId AttachedNodeId { get; }

  public int BufferCapacity { get; }
}

public sealed class ServicePointConfig
{
  public ServicePointConfig(
      ServicePointId servicePointId,
      ServicePointType servicePointType,
      NodeId nodeId,
      ServicePointPassiveSemantics passiveSemantics)
  {
    ServicePointId = servicePointId;
    ServicePointType = servicePointType;
    NodeId = nodeId;
    PassiveSemantics = passiveSemantics;
  }

  public ServicePointId ServicePointId { get; }

  public ServicePointType ServicePointType { get; }

  public NodeId NodeId { get; }

  public ServicePointPassiveSemantics PassiveSemantics { get; }
}

public sealed class DeviceBindingConfig
{
  public DeviceBindingConfig(
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
}

public sealed class EndpointMappingConfig
{
  public EndpointMappingConfig(
      EndpointId endpointId,
      EndpointKind endpointKind,
      StationId? stationId = null,
      ServicePointId? servicePointId = null)
  {
    EndpointId = endpointId;
    EndpointKind = endpointKind;
    StationId = stationId;
    ServicePointId = servicePointId;

    switch (endpointKind)
    {
      case EndpointKind.LoadStation:
      case EndpointKind.UnloadStation:
        if (stationId is null)
        {
          throw new ArgumentException($"{endpointKind} mapping requires a station identifier.", nameof(stationId));
        }

        if (servicePointId is not null)
        {
          throw new ArgumentException($"{endpointKind} mapping cannot reference a service point.", nameof(servicePointId));
        }

        break;
      case EndpointKind.ChargePoint:
      case EndpointKind.ServicePoint:
        if (servicePointId is null)
        {
          throw new ArgumentException($"{endpointKind} mapping requires a service point identifier.", nameof(servicePointId));
        }

        if (stationId is not null)
        {
          throw new ArgumentException($"{endpointKind} mapping cannot reference a station.", nameof(stationId));
        }

        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(endpointKind), endpointKind, "Unsupported endpoint kind.");
    }
  }

  public EndpointId EndpointId { get; }

  public EndpointKind EndpointKind { get; }

  public StationId? StationId { get; }

  public ServicePointId? ServicePointId { get; }
}

public enum ServicePointType
{
  Charge,
  Service
}

public enum ServicePointPassiveSemantics
{
  ArrivalConfirmsEngagement
}

public enum EndpointKind
{
  LoadStation,
  UnloadStation,
  ChargePoint,
  ServicePoint
}
