using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;
using System.Collections.ObjectModel;

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence;

public sealed class TopologyConfigurationRecordSet
{
  public TopologyConfigurationRecordSet(
      TopologyVersionRecord topologyVersion,
      IEnumerable<TopologyLevelRecord>? levels,
      IEnumerable<TopologyNodeRecord>? nodes,
      IEnumerable<TopologyEdgeRecord>? edges,
      IEnumerable<TopologyShaftRecord>? shafts,
      IEnumerable<TopologyShaftStopRecord>? shaftStops,
      IEnumerable<TopologyStationRecord>? stations,
      IEnumerable<TopologyServicePointRecord>? servicePoints,
      IEnumerable<DeviceBindingRecord>? deviceBindings,
      IEnumerable<EndpointMappingRecord>? endpointMappings)
  {
    TopologyVersion = topologyVersion ?? throw new ArgumentNullException(nameof(topologyVersion));
    Levels = Materialize(levels);
    Nodes = Materialize(nodes);
    Edges = Materialize(edges);
    Shafts = Materialize(shafts);
    ShaftStops = Materialize(shaftStops);
    Stations = Materialize(stations);
    ServicePoints = Materialize(servicePoints);
    DeviceBindings = Materialize(deviceBindings);
    EndpointMappings = Materialize(endpointMappings);
  }

  public TopologyVersionRecord TopologyVersion { get; }

  public IReadOnlyList<TopologyLevelRecord> Levels { get; }

  public IReadOnlyList<TopologyNodeRecord> Nodes { get; }

  public IReadOnlyList<TopologyEdgeRecord> Edges { get; }

  public IReadOnlyList<TopologyShaftRecord> Shafts { get; }

  public IReadOnlyList<TopologyShaftStopRecord> ShaftStops { get; }

  public IReadOnlyList<TopologyStationRecord> Stations { get; }

  public IReadOnlyList<TopologyServicePointRecord> ServicePoints { get; }

  public IReadOnlyList<DeviceBindingRecord> DeviceBindings { get; }

  public IReadOnlyList<EndpointMappingRecord> EndpointMappings { get; }

  private static ReadOnlyCollection<T> Materialize<T>(IEnumerable<T>? values) =>
      Array.AsReadOnly((values ?? Enumerable.Empty<T>()).ToArray());
}

public static class TopologyConfigurationPersistenceMapper
{
  public static TopologyConfigurationRecordSet ToRecordSet(
      WarehouseTopologyConfig config,
      string topologyVersionId,
      string sourceHash,
      bool isActive,
      DateTimeOffset activatedAt)
  {
    ArgumentNullException.ThrowIfNull(config);
    ArgumentException.ThrowIfNullOrWhiteSpace(topologyVersionId);
    ArgumentException.ThrowIfNullOrWhiteSpace(sourceHash);

    var normalizedTopologyVersionId = topologyVersionId.Trim();

    return new TopologyConfigurationRecordSet(
        new TopologyVersionRecord
        {
          TopologyVersionId = normalizedTopologyVersionId,
          TopologyId = config.TopologyId.Value,
          Version = config.Version,
          SourceHash = sourceHash.Trim(),
          IsActive = isActive,
          ActivatedAt = activatedAt
        },
        config.Levels.Select(level => new TopologyLevelRecord
        {
          TopologyVersionId = normalizedTopologyVersionId,
          LevelId = level.LevelId.Value,
          Ordinal = level.Ordinal,
          Name = level.Name
        }),
        config.Nodes.Select(node => new TopologyNodeRecord
        {
          TopologyVersionId = normalizedTopologyVersionId,
          NodeId = node.NodeId.Value,
          NodeType = node.NodeType,
          LevelId = node.LevelId?.Value,
          Tags = node.Tags.ToArray(),
          StationId = node.StationId?.Value,
          ShaftId = node.ShaftId?.Value,
          ServicePointId = node.ServicePointId?.Value
        }),
        config.Edges.Select(edge => new TopologyEdgeRecord
        {
          TopologyVersionId = normalizedTopologyVersionId,
          EdgeId = edge.EdgeId.Value,
          FromNodeId = edge.FromNodeId.Value,
          ToNodeId = edge.ToNodeId.Value,
          TraversalMode = edge.TraversalMode,
          Weight = edge.Weight
        }),
        config.Shafts.Select(shaft => new TopologyShaftRecord
        {
          TopologyVersionId = normalizedTopologyVersionId,
          ShaftId = shaft.ShaftId.Value,
          CarrierDeviceId = shaft.CarrierDeviceId.Value,
          SlotCount = shaft.SlotCount
        }),
        config.Shafts.SelectMany(shaft => shaft.Stops.Select(stop => new TopologyShaftStopRecord
        {
          TopologyVersionId = normalizedTopologyVersionId,
          ShaftId = shaft.ShaftId.Value,
          LevelId = stop.LevelId.Value,
          CarrierNodeId = stop.CarrierNodeId.Value,
          TransferPointId = stop.TransferPointId.Value
        })),
        config.Stations.Select(station => new TopologyStationRecord
        {
          TopologyVersionId = normalizedTopologyVersionId,
          StationId = station.StationId.Value,
          StationType = station.StationType,
          AttachedNodeId = station.AttachedNodeId.Value,
          ControlMode = station.ControlMode,
          BufferCapacity = station.BufferCapacity
        }),
        config.ServicePoints.Select(servicePoint => new TopologyServicePointRecord
        {
          TopologyVersionId = normalizedTopologyVersionId,
          ServicePointId = servicePoint.ServicePointId.Value,
          NodeId = servicePoint.NodeId.Value,
          ServicePointType = servicePoint.ServicePointType,
          PassiveSemantics = servicePoint.PassiveSemantics
        }),
        config.DeviceBindings.Select(binding => new DeviceBindingRecord
        {
          TopologyVersionId = normalizedTopologyVersionId,
          DeviceId = binding.DeviceId.Value,
          DeviceFamily = binding.Family,
          InitialNodeId = binding.InitialNodeId?.Value,
          HomeNodeId = binding.HomeNodeId?.Value,
          ShaftId = binding.ShaftId?.Value
        }),
        config.EndpointMappings.Select(mapping => new EndpointMappingRecord
        {
          TopologyVersionId = normalizedTopologyVersionId,
          EndpointId = mapping.EndpointId.Value,
          EndpointKind = mapping.EndpointKind,
          StationId = mapping.StationId?.Value,
          ServicePointId = mapping.ServicePointId?.Value
        }));
  }

  public static WarehouseTopologyConfig ToConfiguration(TopologyConfigurationRecordSet recordSet)
  {
    ArgumentNullException.ThrowIfNull(recordSet);

    var levelsById = recordSet.Levels.ToDictionary(static level => level.LevelId, StringComparer.Ordinal);

    return new WarehouseTopologyConfig(
        new TopologyId(recordSet.TopologyVersion.TopologyId),
        recordSet.TopologyVersion.Version,
        recordSet.Levels
            .OrderBy(static level => level.Ordinal)
            .ThenBy(static level => level.LevelId, StringComparer.Ordinal)
            .Select(static level => new LevelConfig(
                new LevelId(level.LevelId),
                level.Ordinal,
                level.Name)),
        recordSet.Nodes
            .OrderBy(static node => node.NodeId, StringComparer.Ordinal)
            .Select(node => new TopologyNodeConfig(
                new NodeId(node.NodeId),
                node.NodeType,
                ToOptionalLevelId(node.LevelId),
                node.Tags ?? [],
                ToOptionalStationId(node.StationId),
                ToOptionalShaftId(node.ShaftId),
                ToOptionalServicePointId(node.ServicePointId))),
        recordSet.Edges
            .OrderBy(static edge => edge.EdgeId, StringComparer.Ordinal)
            .Select(static edge => new TopologyEdgeConfig(
                new EdgeId(edge.EdgeId),
                new NodeId(edge.FromNodeId),
                new NodeId(edge.ToNodeId),
                edge.TraversalMode,
                edge.Weight)),
        recordSet.Shafts
            .OrderBy(static shaft => shaft.ShaftId, StringComparer.Ordinal)
            .Select(shaft => new CarrierShaftConfig(
                new ShaftId(shaft.ShaftId),
                new DeviceId(shaft.CarrierDeviceId),
                shaft.SlotCount,
                recordSet.ShaftStops
                    .Where(stop => stop.ShaftId == shaft.ShaftId)
                    .OrderBy(stop => ResolveLevelOrdinal(stop.LevelId, levelsById))
                    .ThenBy(static stop => stop.LevelId, StringComparer.Ordinal)
                    .Select(static stop => new CarrierShaftStopConfig(
                        new LevelId(stop.LevelId),
                        new NodeId(stop.CarrierNodeId),
                        new NodeId(stop.TransferPointId))))),
        recordSet.Stations
            .OrderBy(static station => station.StationId, StringComparer.Ordinal)
            .Select(static station => new StationConfig(
                new StationId(station.StationId),
                station.StationType,
                station.ControlMode,
                new NodeId(station.AttachedNodeId),
                station.BufferCapacity)),
        recordSet.ServicePoints
            .OrderBy(static servicePoint => servicePoint.ServicePointId, StringComparer.Ordinal)
            .Select(static servicePoint => new ServicePointConfig(
                new ServicePointId(servicePoint.ServicePointId),
                servicePoint.ServicePointType,
                new NodeId(servicePoint.NodeId),
                servicePoint.PassiveSemantics)),
        recordSet.DeviceBindings
            .OrderBy(static binding => binding.DeviceId, StringComparer.Ordinal)
            .Select(static binding => new DeviceBindingConfig(
                new DeviceId(binding.DeviceId),
                binding.DeviceFamily,
                ToOptionalNodeId(binding.InitialNodeId),
                ToOptionalNodeId(binding.HomeNodeId),
                ToOptionalShaftId(binding.ShaftId))),
        recordSet.EndpointMappings
            .OrderBy(static mapping => mapping.EndpointId, StringComparer.Ordinal)
            .Select(static mapping => new EndpointMappingConfig(
                new EndpointId(mapping.EndpointId),
                mapping.EndpointKind,
                ToOptionalStationId(mapping.StationId),
                ToOptionalServicePointId(mapping.ServicePointId))));
  }

  private static int ResolveLevelOrdinal(
      string levelId,
      Dictionary<string, TopologyLevelRecord> levelsById) =>
      levelsById.TryGetValue(levelId, out var level)
          ? level.Ordinal
          : int.MaxValue;

  private static LevelId? ToOptionalLevelId(string? value) =>
      value is null ? null : new LevelId(value);

  private static NodeId? ToOptionalNodeId(string? value) =>
      value is null ? null : new NodeId(value);

  private static StationId? ToOptionalStationId(string? value) =>
      value is null ? null : new StationId(value);

  private static ShaftId? ToOptionalShaftId(string? value) =>
      value is null ? null : new ShaftId(value);

  private static ServicePointId? ToOptionalServicePointId(string? value) =>
      value is null ? null : new ServicePointId(value);
}
