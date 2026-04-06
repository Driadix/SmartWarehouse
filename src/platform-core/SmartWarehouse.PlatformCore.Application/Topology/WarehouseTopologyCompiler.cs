using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Application.Topology;

public interface IWarehouseTopologyCompiler
{
  CompiledWarehouseTopology Compile(WarehouseTopologyConfig config);
}

public sealed class WarehouseTopologyCompiler(IWarehouseTopologyConfigValidator validator) : IWarehouseTopologyCompiler
{
  public CompiledWarehouseTopology Compile(WarehouseTopologyConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    validator.EnsureValid(config);

    var levelsById = config.Levels.ToDictionary(static level => level.LevelId);
    var stationEndpointIds = BuildEndpointIdLookup(
        config.EndpointMappings,
        static mapping => mapping.StationId);
    var servicePointEndpointIds = BuildEndpointIdLookup(
        config.EndpointMappings,
        static mapping => mapping.ServicePointId);

    var compiledLevels = config.Levels
        .Select(static level => new CompiledTopologyLevel(level.LevelId, level.Ordinal, level.Name))
        .ToArray();

    var compiledNodesById = config.Nodes
        .Select(node => CompileNode(node, levelsById))
        .ToDictionary(static node => node.NodeId);

    var compiledStationsById = config.Stations
        .Select(station => CompileStation(station, compiledNodesById, stationEndpointIds))
        .ToDictionary(static station => station.StationId);

    var compiledServicePointsById = config.ServicePoints
        .Select(servicePoint => CompileServicePoint(servicePoint, compiledNodesById, servicePointEndpointIds))
        .ToDictionary(static servicePoint => servicePoint.ServicePointId);

    var compiledShafts = config.Shafts
        .Select(shaft => CompileShaft(shaft, levelsById, compiledNodesById))
        .ToArray();

    var compiledDeviceBindings = config.DeviceBindings
        .Select(static binding => new CompiledDeviceBinding(
            binding.DeviceId,
            binding.Family,
            binding.InitialNodeId,
            binding.HomeNodeId,
            binding.ShaftId))
        .ToArray();

    var compiledEdges = config.Edges
        .Select(edge => new CompiledTopologyEdge(
            edge.EdgeId,
            compiledNodesById[edge.FromNodeId],
            compiledNodesById[edge.ToNodeId],
            edge.TraversalMode,
            edge.Weight))
        .ToArray();

    var compiledEndpoints = config.EndpointMappings
        .Select(mapping => CompileEndpoint(mapping, compiledStationsById, compiledServicePointsById))
        .ToArray();

    return new CompiledWarehouseTopology(
        config.TopologyId,
        config.Version,
        compiledLevels,
        compiledNodesById.Values,
        compiledEdges,
        compiledShafts,
        compiledStationsById.Values,
        compiledServicePointsById.Values,
        compiledDeviceBindings,
        compiledEndpoints);
  }

  private static Dictionary<TKey, IReadOnlyList<EndpointId>> BuildEndpointIdLookup<TKey>(
      IEnumerable<EndpointMappingConfig> endpointMappings,
      Func<EndpointMappingConfig, TKey?> keySelector)
      where TKey : struct
  {
    return endpointMappings
        .Select(mapping => (Key: keySelector(mapping), mapping.EndpointId))
        .Where(static entry => entry.Key is not null)
        .GroupBy(static entry => entry.Key!.Value)
        .ToDictionary(
            static group => group.Key,
            static group => (IReadOnlyList<EndpointId>)Array.AsReadOnly(group.Select(static entry => entry.EndpointId).ToArray()));
  }

  private static CompiledTopologyNode CompileNode(
      TopologyNodeConfig node,
      Dictionary<LevelId, LevelConfig> levelsById)
  {
    int? levelOrdinal = node.LevelId is { } levelId
        ? levelsById[levelId].Ordinal
        : null;

    return new CompiledTopologyNode(
        node.NodeId,
        node.NodeType,
        node.LevelId,
        levelOrdinal,
        node.Tags,
        node.StationId,
        node.ShaftId,
        node.ServicePointId);
  }

  private static CompiledStationBoundary CompileStation(
      StationConfig station,
      Dictionary<NodeId, CompiledTopologyNode> nodesById,
      Dictionary<StationId, IReadOnlyList<EndpointId>> endpointIdsByStationId)
  {
    var endpointIds = endpointIdsByStationId.TryGetValue(station.StationId, out var boundEndpointIds)
        ? boundEndpointIds
        : Array.Empty<EndpointId>();

    return new CompiledStationBoundary(
        station.StationId,
        station.StationType,
        nodesById[station.AttachedNodeId],
        station.BufferCapacity,
        endpointIds);
  }

  private static CompiledServicePoint CompileServicePoint(
      ServicePointConfig servicePoint,
      Dictionary<NodeId, CompiledTopologyNode> nodesById,
      Dictionary<ServicePointId, IReadOnlyList<EndpointId>> endpointIdsByServicePointId)
  {
    var endpointIds = endpointIdsByServicePointId.TryGetValue(servicePoint.ServicePointId, out var boundEndpointIds)
        ? boundEndpointIds
        : Array.Empty<EndpointId>();

    return new CompiledServicePoint(
        servicePoint.ServicePointId,
        servicePoint.ServicePointType,
        nodesById[servicePoint.NodeId],
        servicePoint.PassiveSemantics,
        endpointIds);
  }

  private static CompiledCarrierShaft CompileShaft(
      CarrierShaftConfig shaft,
      Dictionary<LevelId, LevelConfig> levelsById,
      Dictionary<NodeId, CompiledTopologyNode> nodesById)
  {
    var compiledStops = shaft.Stops
        .Select(stop => new CompiledCarrierShaftStop(
            shaft.ShaftId,
            shaft.CarrierDeviceId,
            stop.LevelId,
            levelsById[stop.LevelId].Ordinal,
            nodesById[stop.CarrierNodeId],
            nodesById[stop.TransferPointId]))
        .ToArray();

    return new CompiledCarrierShaft(
        shaft.ShaftId,
        shaft.CarrierDeviceId,
        shaft.SlotCount,
        compiledStops);
  }

  private static CompiledEndpointBinding CompileEndpoint(
      EndpointMappingConfig endpoint,
      Dictionary<StationId, CompiledStationBoundary> stationsById,
      Dictionary<ServicePointId, CompiledServicePoint> servicePointsById)
  {
    return endpoint.EndpointKind switch
    {
      EndpointKind.LoadStation or EndpointKind.UnloadStation => new CompiledEndpointBinding(
          endpoint.EndpointId,
          endpoint.EndpointKind,
          stationBoundary: stationsById[endpoint.StationId!.Value]),
      EndpointKind.ChargePoint or EndpointKind.ServicePoint => new CompiledEndpointBinding(
          endpoint.EndpointId,
          endpoint.EndpointKind,
          servicePoint: servicePointsById[endpoint.ServicePointId!.Value]),
      _ => throw new InvalidOperationException($"Unsupported endpoint kind '{endpoint.EndpointKind}'.")
    };
  }
}

public static class TopologyServiceCollectionExtensions
{
  public static IServiceCollection AddWarehouseTopologyServices(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.TryAddSingleton<IWarehouseTopologyConfigLoader, YamlWarehouseTopologyConfigLoader>();
    services.TryAddSingleton<IWarehouseTopologyConfigValidator, WarehouseTopologyConfigValidator>();
    services.TryAddSingleton<IWarehouseTopologyCompiler, WarehouseTopologyCompiler>();

    return services;
  }
}
