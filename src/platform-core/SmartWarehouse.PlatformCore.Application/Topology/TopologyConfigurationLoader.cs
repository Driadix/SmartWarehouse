using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SmartWarehouse.PlatformCore.Application.Topology;

public interface IWarehouseTopologyConfigLoader
{
  WarehouseTopologyConfig Load(string yamlContent);

  WarehouseTopologyConfig LoadFromFile(string filePath);
}

public sealed class YamlWarehouseTopologyConfigLoader : IWarehouseTopologyConfigLoader
{
  private readonly IDeserializer _deserializer;

  public YamlWarehouseTopologyConfigLoader()
      : this(
          new DeserializerBuilder()
              .WithNamingConvention(CamelCaseNamingConvention.Instance)
              .Build())
  {
  }

  internal YamlWarehouseTopologyConfigLoader(IDeserializer deserializer)
  {
    _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
  }

  public WarehouseTopologyConfig Load(string yamlContent)
  {
    if (string.IsNullOrWhiteSpace(yamlContent))
    {
      throw new TopologyConfigurationException("Topology YAML content cannot be null, empty, or whitespace.");
    }

    try
    {
      var document = _deserializer.Deserialize<YamlWarehouseTopologyDocument>(yamlContent);

      if (document is null)
      {
        throw new TopologyConfigurationException("Topology YAML content did not produce a document.");
      }

      return MapDocument(document);
    }
    catch (TopologyConfigurationException)
    {
      throw;
    }
    catch (YamlException exception)
    {
      throw new TopologyConfigurationException("Failed to parse topology YAML content.", exception);
    }
    catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
    {
      throw new TopologyConfigurationException("Topology YAML content is invalid.", exception);
    }
  }

  public WarehouseTopologyConfig LoadFromFile(string filePath)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

    try
    {
      return Load(File.ReadAllText(filePath));
    }
    catch (FileNotFoundException)
    {
      throw;
    }
    catch (DirectoryNotFoundException)
    {
      throw;
    }
    catch (TopologyConfigurationException)
    {
      throw;
    }
    catch (Exception exception)
    {
      throw new TopologyConfigurationException($"Failed to load topology YAML from file '{filePath}'.", exception);
    }
  }

  private static WarehouseTopologyConfig MapDocument(YamlWarehouseTopologyDocument document)
  {
    return new WarehouseTopologyConfig(
        topologyId: CreateRequired(document.TopologyId, static value => new TopologyId(value), "topologyId"),
        version: document.Version,
        levels: (document.Levels ?? []).Select(MapLevel),
        nodes: (document.Nodes ?? []).Select(MapNode),
        edges: (document.Edges ?? []).Select(MapEdge),
        shafts: (document.Shafts ?? []).Select(MapShaft),
        stations: (document.Stations ?? []).Select(MapStation),
        servicePoints: (document.ServicePoints ?? []).Select(MapServicePoint),
        deviceBindings: (document.DeviceBindings ?? []).Select(MapDeviceBinding),
        endpointMappings: (document.EndpointMappings ?? []).Select(MapEndpointMapping));
  }

  private static LevelConfig MapLevel(YamlLevelConfig level)
  {
    ArgumentNullException.ThrowIfNull(level);

    return new LevelConfig(
        levelId: CreateRequired(level.LevelId, static value => new LevelId(value), "levels[].levelId"),
        ordinal: level.Ordinal,
        name: level.Name);
  }

  private static TopologyNodeConfig MapNode(YamlTopologyNodeConfig node)
  {
    ArgumentNullException.ThrowIfNull(node);

    return new TopologyNodeConfig(
        nodeId: CreateRequired(node.NodeId, static value => new NodeId(value), "nodes[].nodeId"),
        nodeType: ParseNodeType(node.NodeType),
        levelId: CreateOptional(node.LevelId, static value => new LevelId(value), "nodes[].levelId"),
        tags: node.Tags,
        stationId: CreateOptional(node.StationId, static value => new StationId(value), "nodes[].stationId"),
        shaftId: CreateOptional(node.ShaftId, static value => new ShaftId(value), "nodes[].shaftId"),
        servicePointId: CreateOptional(node.ServicePointId, static value => new ServicePointId(value), "nodes[].servicePointId"));
  }

  private static TopologyEdgeConfig MapEdge(YamlTopologyEdgeConfig edge)
  {
    ArgumentNullException.ThrowIfNull(edge);

    return new TopologyEdgeConfig(
        edgeId: CreateRequired(edge.EdgeId, static value => new EdgeId(value), "edges[].edgeId"),
        fromNodeId: CreateRequired(edge.FromNodeId, static value => new NodeId(value), "edges[].fromNodeId"),
        toNodeId: CreateRequired(edge.ToNodeId, static value => new NodeId(value), "edges[].toNodeId"),
        traversalMode: ParseTraversalMode(edge.TraversalMode),
        weight: edge.Weight);
  }

  private static CarrierShaftConfig MapShaft(YamlCarrierShaftConfig shaft)
  {
    ArgumentNullException.ThrowIfNull(shaft);

    return new CarrierShaftConfig(
        shaftId: CreateRequired(shaft.ShaftId, static value => new ShaftId(value), "shafts[].shaftId"),
        carrierDeviceId: CreateRequired(shaft.CarrierDeviceId, static value => new DeviceId(value), "shafts[].carrierDeviceId"),
        slotCount: shaft.SlotCount,
        stops: (shaft.Stops ?? []).Select(MapShaftStop));
  }

  private static CarrierShaftStopConfig MapShaftStop(YamlCarrierShaftStopConfig stop)
  {
    ArgumentNullException.ThrowIfNull(stop);

    return new CarrierShaftStopConfig(
        levelId: CreateRequired(stop.LevelId, static value => new LevelId(value), "shafts[].stops[].levelId"),
        carrierNodeId: CreateRequired(stop.CarrierNodeId, static value => new NodeId(value), "shafts[].stops[].carrierNodeId"),
        transferPointId: CreateRequired(stop.TransferPointId, static value => new NodeId(value), "shafts[].stops[].transferPointId"));
  }

  private static StationConfig MapStation(YamlStationConfig station)
  {
    ArgumentNullException.ThrowIfNull(station);

    return new StationConfig(
        stationId: CreateRequired(station.StationId, static value => new StationId(value), "stations[].stationId"),
        stationType: ParseStationType(station.StationType),
        controlMode: ParseStationControlMode(station.ControlMode),
        attachedNodeId: CreateRequired(station.AttachedNodeId, static value => new NodeId(value), "stations[].attachedNodeId"),
        bufferCapacity: station.BufferCapacity);
  }

  private static ServicePointConfig MapServicePoint(YamlServicePointConfig servicePoint)
  {
    ArgumentNullException.ThrowIfNull(servicePoint);

    return new ServicePointConfig(
        servicePointId: CreateRequired(servicePoint.ServicePointId, static value => new ServicePointId(value), "servicePoints[].servicePointId"),
        servicePointType: ParseServicePointType(servicePoint.ServicePointType),
        nodeId: CreateRequired(servicePoint.NodeId, static value => new NodeId(value), "servicePoints[].nodeId"),
        passiveSemantics: ParseServicePointPassiveSemantics(servicePoint.PassiveSemantics));
  }

  private static DeviceBindingConfig MapDeviceBinding(YamlDeviceBindingConfig binding)
  {
    ArgumentNullException.ThrowIfNull(binding);

    return new DeviceBindingConfig(
        deviceId: CreateRequired(binding.DeviceId, static value => new DeviceId(value), "deviceBindings[].deviceId"),
        family: ParseDeviceFamily(binding.Family),
        initialNodeId: CreateOptional(binding.InitialNodeId, static value => new NodeId(value), "deviceBindings[].initialNodeId"),
        homeNodeId: CreateOptional(binding.HomeNodeId, static value => new NodeId(value), "deviceBindings[].homeNodeId"),
        shaftId: CreateOptional(binding.ShaftId, static value => new ShaftId(value), "deviceBindings[].shaftId"));
  }

  private static EndpointMappingConfig MapEndpointMapping(YamlEndpointMappingConfig endpointMapping)
  {
    ArgumentNullException.ThrowIfNull(endpointMapping);

    return new EndpointMappingConfig(
        endpointId: CreateRequired(endpointMapping.EndpointId, static value => new EndpointId(value), "endpointMappings[].endpointId"),
        endpointKind: ParseEndpointKind(endpointMapping.EndpointKind),
        stationId: CreateOptional(endpointMapping.StationId, static value => new StationId(value), "endpointMappings[].stationId"),
        servicePointId: CreateOptional(endpointMapping.ServicePointId, static value => new ServicePointId(value), "endpointMappings[].servicePointId"));
  }

  private static TIdentifier CreateRequired<TIdentifier>(
      string? value,
      Func<string, TIdentifier> factory,
      string propertyName)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      throw new TopologyConfigurationException($"Required topology property '{propertyName}' is missing.");
    }

    try
    {
      return factory(value);
    }
    catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
    {
      throw new TopologyConfigurationException($"Topology property '{propertyName}' is invalid.", exception);
    }
  }

  private static TIdentifier? CreateOptional<TIdentifier>(
      string? value,
      Func<string, TIdentifier> factory,
      string propertyName)
      where TIdentifier : struct
  {
    if (value is null)
    {
      return null;
    }

    if (string.IsNullOrWhiteSpace(value))
    {
      throw new TopologyConfigurationException($"Topology property '{propertyName}' cannot be whitespace.");
    }

    try
    {
      return factory(value);
    }
    catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
    {
      throw new TopologyConfigurationException($"Topology property '{propertyName}' is invalid.", exception);
    }
  }

  private static NodeType ParseNodeType(string? value) =>
      value switch
      {
        "TravelNode" => NodeType.TravelNode,
        "SwitchNode" => NodeType.SwitchNode,
        "TransferPoint" => NodeType.TransferPoint,
        "CarrierNode" => NodeType.CarrierNode,
        "StationNode" => NodeType.StationNode,
        "ChargeNode" => NodeType.ChargeNode,
        "ServiceNode" => NodeType.ServiceNode,
        _ => throw new TopologyConfigurationException($"Unsupported nodeType '{value ?? "<null>"}'.")
      };

  private static EdgeTraversalMode ParseTraversalMode(string? value) =>
      value switch
      {
        "OPEN" => EdgeTraversalMode.Open,
        "CARRIER_ONLY" => EdgeTraversalMode.CarrierOnly,
        "RESTRICTED" => EdgeTraversalMode.Restricted,
        _ => throw new TopologyConfigurationException($"Unsupported traversalMode '{value ?? "<null>"}'.")
      };

  private static StationType ParseStationType(string? value) =>
      value switch
      {
        "LOAD" => StationType.Load,
        "UNLOAD" => StationType.Unload,
        _ => throw new TopologyConfigurationException($"Unsupported stationType '{value ?? "<null>"}'.")
      };

  private static StationControlMode ParseStationControlMode(string? value) =>
      value switch
      {
        "PASSIVE" => StationControlMode.Passive,
        "ACTIVE" => StationControlMode.Active,
        _ => throw new TopologyConfigurationException($"Unsupported controlMode '{value ?? "<null>"}'.")
      };

  private static ServicePointType ParseServicePointType(string? value) =>
      value switch
      {
        "CHARGE" => ServicePointType.Charge,
        "SERVICE" => ServicePointType.Service,
        _ => throw new TopologyConfigurationException($"Unsupported servicePointType '{value ?? "<null>"}'.")
      };

  private static ServicePointPassiveSemantics ParseServicePointPassiveSemantics(string? value) =>
      value switch
      {
        "ARRIVAL_CONFIRMS_ENGAGEMENT" => ServicePointPassiveSemantics.ArrivalConfirmsEngagement,
        _ => throw new TopologyConfigurationException($"Unsupported passiveSemantics '{value ?? "<null>"}'.")
      };

  private static DeviceFamily ParseDeviceFamily(string? value) =>
      value switch
      {
        "Shuttle3D" => DeviceFamily.Shuttle3D,
        "HybridLift" => DeviceFamily.HybridLift,
        _ => throw new TopologyConfigurationException($"Unsupported device family '{value ?? "<null>"}'.")
      };

  private static EndpointKind ParseEndpointKind(string? value) =>
      value switch
      {
        "LOAD_STATION" => EndpointKind.LoadStation,
        "UNLOAD_STATION" => EndpointKind.UnloadStation,
        "CHARGE_POINT" => EndpointKind.ChargePoint,
        "SERVICE_POINT" => EndpointKind.ServicePoint,
        _ => throw new TopologyConfigurationException($"Unsupported endpointKind '{value ?? "<null>"}'.")
      };

  private sealed class YamlWarehouseTopologyDocument
  {
    public string? TopologyId { get; set; }

    public int Version { get; set; }

    public List<YamlLevelConfig>? Levels { get; set; }

    public List<YamlTopologyNodeConfig>? Nodes { get; set; }

    public List<YamlTopologyEdgeConfig>? Edges { get; set; }

    public List<YamlCarrierShaftConfig>? Shafts { get; set; }

    public List<YamlStationConfig>? Stations { get; set; }

    public List<YamlServicePointConfig>? ServicePoints { get; set; }

    public List<YamlDeviceBindingConfig>? DeviceBindings { get; set; }

    public List<YamlEndpointMappingConfig>? EndpointMappings { get; set; }
  }

  private sealed class YamlLevelConfig
  {
    public string? LevelId { get; set; }

    public int Ordinal { get; set; }

    public string? Name { get; set; }
  }

  private sealed class YamlTopologyNodeConfig
  {
    public string? NodeId { get; set; }

    public string? NodeType { get; set; }

    public string? LevelId { get; set; }

    public List<string>? Tags { get; set; }

    public string? StationId { get; set; }

    public string? ShaftId { get; set; }

    public string? ServicePointId { get; set; }
  }

  private sealed class YamlTopologyEdgeConfig
  {
    public string? EdgeId { get; set; }

    public string? FromNodeId { get; set; }

    public string? ToNodeId { get; set; }

    public string? TraversalMode { get; set; }

    public decimal Weight { get; set; }
  }

  private sealed class YamlCarrierShaftConfig
  {
    public string? ShaftId { get; set; }

    public string? CarrierDeviceId { get; set; }

    public int SlotCount { get; set; }

    public List<YamlCarrierShaftStopConfig>? Stops { get; set; }
  }

  private sealed class YamlCarrierShaftStopConfig
  {
    public string? LevelId { get; set; }

    public string? CarrierNodeId { get; set; }

    public string? TransferPointId { get; set; }
  }

  private sealed class YamlStationConfig
  {
    public string? StationId { get; set; }

    public string? StationType { get; set; }

    public string? ControlMode { get; set; }

    public string? AttachedNodeId { get; set; }

    public int BufferCapacity { get; set; }
  }

  private sealed class YamlServicePointConfig
  {
    public string? ServicePointId { get; set; }

    public string? ServicePointType { get; set; }

    public string? NodeId { get; set; }

    public string? PassiveSemantics { get; set; }
  }

  private sealed class YamlDeviceBindingConfig
  {
    public string? DeviceId { get; set; }

    public string? Family { get; set; }

    public string? InitialNodeId { get; set; }

    public string? HomeNodeId { get; set; }

    public string? ShaftId { get; set; }
  }

  private sealed class YamlEndpointMappingConfig
  {
    public string? EndpointId { get; set; }

    public string? EndpointKind { get; set; }

    public string? StationId { get; set; }

    public string? ServicePointId { get; set; }
  }
}
