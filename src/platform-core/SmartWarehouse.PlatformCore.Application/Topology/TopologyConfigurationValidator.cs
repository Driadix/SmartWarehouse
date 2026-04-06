using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Application.Topology;

public interface IWarehouseTopologyConfigValidator
{
  IReadOnlyList<TopologyValidationError> Validate(WarehouseTopologyConfig config);

  void EnsureValid(WarehouseTopologyConfig config);
}

public sealed class WarehouseTopologyConfigValidator : IWarehouseTopologyConfigValidator
{
  public IReadOnlyList<TopologyValidationError> Validate(WarehouseTopologyConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    var errors = new List<TopologyValidationError>();
    var levelsById = config.Levels.ToDictionary(static level => level.LevelId);
    var nodesById = config.Nodes.ToDictionary(static node => node.NodeId);
    var shaftsById = config.Shafts.ToDictionary(static shaft => shaft.ShaftId);
    var stationsById = config.Stations.ToDictionary(static station => station.StationId);
    var servicePointsById = config.ServicePoints.ToDictionary(static servicePoint => servicePoint.ServicePointId);
    var deviceBindingsById = config.DeviceBindings.ToDictionary(static binding => binding.DeviceId);

    ValidateNodeReferences(config, levelsById, shaftsById, stationsById, servicePointsById, errors);
    ValidateStations(config, nodesById, errors);
    ValidateServicePoints(config, nodesById, errors);
    ValidateShafts(config, levelsById, nodesById, deviceBindingsById, errors);
    ValidateEdges(config, nodesById, errors);
    ValidateDeviceBindings(config, nodesById, shaftsById, errors);
    ValidateEndpointMappings(config, stationsById, servicePointsById, deviceBindingsById, errors);

    return Array.AsReadOnly(errors.ToArray());
  }

  public void EnsureValid(WarehouseTopologyConfig config)
  {
    var errors = Validate(config);

    if (errors.Count > 0)
    {
      throw new TopologyValidationException(errors);
    }
  }

  private static void ValidateNodeReferences(
      WarehouseTopologyConfig config,
      Dictionary<LevelId, LevelConfig> levelsById,
      Dictionary<ShaftId, CarrierShaftConfig> shaftsById,
      Dictionary<StationId, StationConfig> stationsById,
      Dictionary<ServicePointId, ServicePointConfig> servicePointsById,
      List<TopologyValidationError> errors)
  {
    foreach (var node in config.Nodes)
    {
      if (node.LevelId is { } levelId && !levelsById.ContainsKey(levelId))
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.UnknownLevelReference,
            $"Node '{node.NodeId}' references unknown level '{levelId}'."));
      }

      if (node.ShaftId is { } shaftId && !shaftsById.ContainsKey(shaftId))
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.UnknownShaftReference,
            $"Node '{node.NodeId}' references unknown shaft '{shaftId}'."));
      }

      if (node.StationId is { } stationId && !stationsById.ContainsKey(stationId))
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.UnknownStationReference,
            $"Node '{node.NodeId}' references unknown station '{stationId}'."));
      }

      if (node.ServicePointId is { } servicePointId && !servicePointsById.ContainsKey(servicePointId))
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.UnknownServicePointReference,
            $"Node '{node.NodeId}' references unknown service point '{servicePointId}'."));
      }
    }
  }

  private static void ValidateStations(
      WarehouseTopologyConfig config,
      Dictionary<NodeId, TopologyNodeConfig> nodesById,
      List<TopologyValidationError> errors)
  {
    foreach (var station in config.Stations)
    {
      if (!nodesById.TryGetValue(station.AttachedNodeId, out var node))
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.UnknownNodeReference,
            $"Station '{station.StationId}' references unknown attached node '{station.AttachedNodeId}'."));
        continue;
      }

      if (node.NodeType != NodeType.StationNode || node.StationId != station.StationId)
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.InvalidStationAttachment,
            $"Station '{station.StationId}' must be attached to a matching StationNode."));
      }
    }
  }

  private static void ValidateServicePoints(
      WarehouseTopologyConfig config,
      Dictionary<NodeId, TopologyNodeConfig> nodesById,
      List<TopologyValidationError> errors)
  {
    foreach (var servicePoint in config.ServicePoints)
    {
      if (!nodesById.TryGetValue(servicePoint.NodeId, out var node))
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.UnknownNodeReference,
            $"Service point '{servicePoint.ServicePointId}' references unknown node '{servicePoint.NodeId}'."));
        continue;
      }

      var expectedNodeType = servicePoint.ServicePointType switch
      {
        ServicePointType.Charge => NodeType.ChargeNode,
        ServicePointType.Service => NodeType.ServiceNode,
        _ => throw new InvalidOperationException($"Unsupported service point type '{servicePoint.ServicePointType}'.")
      };

      if (node.NodeType != expectedNodeType || node.ServicePointId != servicePoint.ServicePointId)
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.InvalidServicePointNode,
            $"Service point '{servicePoint.ServicePointId}' must target a matching {expectedNodeType}."));
      }
    }
  }

  private static void ValidateShafts(
      WarehouseTopologyConfig config,
      Dictionary<LevelId, LevelConfig> levelsById,
      Dictionary<NodeId, TopologyNodeConfig> nodesById,
      Dictionary<DeviceId, DeviceBindingConfig> deviceBindingsById,
      List<TopologyValidationError> errors)
  {
    foreach (var shaft in config.Shafts)
    {
      if (!deviceBindingsById.TryGetValue(shaft.CarrierDeviceId, out var binding) ||
          binding.Family != DeviceFamily.HybridLift ||
          binding.ShaftId != shaft.ShaftId)
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.InvalidShaftCarrierDeviceBinding,
            $"Shaft '{shaft.ShaftId}' must reference a matching HybridLift device binding."));
      }

      foreach (var stop in shaft.Stops)
      {
        if (!levelsById.ContainsKey(stop.LevelId))
        {
          errors.Add(new TopologyValidationError(
              TopologyValidationErrorCode.UnknownLevelReference,
              $"Shaft '{shaft.ShaftId}' stop references unknown level '{stop.LevelId}'."));
        }

        ValidateShaftStopNode(
            shaft.ShaftId,
            stop.LevelId,
            stop.CarrierNodeId,
            NodeType.CarrierNode,
            "carrier node",
            nodesById,
            errors);

        ValidateShaftStopNode(
            shaft.ShaftId,
            stop.LevelId,
            stop.TransferPointId,
            NodeType.TransferPoint,
            "transfer point",
            nodesById,
            errors);
      }
    }

    var duplicateCarrierLevels = config.Nodes
        .Where(static node => node.NodeType == NodeType.CarrierNode)
        .GroupBy(static node => new { node.ShaftId, node.LevelId })
        .Where(static group => group.Key.ShaftId is not null && group.Key.LevelId is not null && group.Count() > 1);

    foreach (var duplicateGroup in duplicateCarrierLevels)
    {
      errors.Add(new TopologyValidationError(
          TopologyValidationErrorCode.DuplicateCarrierNodeLevel,
          $"Shaft '{duplicateGroup.Key.ShaftId}' contains more than one CarrierNode on level '{duplicateGroup.Key.LevelId}'."));
    }

    var transferPoints = config.Nodes
        .Where(static node => node.NodeType == NodeType.TransferPoint)
        .Select(static node => (node.ShaftId, node.LevelId))
        .ToHashSet();

    foreach (var carrierNode in config.Nodes.Where(static node => node.NodeType == NodeType.CarrierNode))
    {
      var transferPointKey = (carrierNode.ShaftId, carrierNode.LevelId);

      if (!transferPoints.Contains(transferPointKey))
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.MissingTransferPointForCarrierNode,
            $"CarrierNode '{carrierNode.NodeId}' must have a matching TransferPoint on the same shaft and level."));
      }
    }
  }

  private static void ValidateEdges(
      WarehouseTopologyConfig config,
      Dictionary<NodeId, TopologyNodeConfig> nodesById,
      List<TopologyValidationError> errors)
  {
    foreach (var edge in config.Edges)
    {
      var hasFromNode = nodesById.ContainsKey(edge.FromNodeId);
      var hasToNode = nodesById.ContainsKey(edge.ToNodeId);

      if (!hasFromNode)
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.UnknownNodeReference,
            $"Edge '{edge.EdgeId}' references unknown source node '{edge.FromNodeId}'."));
      }

      if (!hasToNode)
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.UnknownNodeReference,
            $"Edge '{edge.EdgeId}' references unknown target node '{edge.ToNodeId}'."));
      }

      if (!hasFromNode || !hasToNode)
      {
        continue;
      }

      var fromNode = nodesById[edge.FromNodeId];
      var toNode = nodesById[edge.ToNodeId];
      var touchesCarrierNode = fromNode.NodeType == NodeType.CarrierNode || toNode.NodeType == NodeType.CarrierNode;

      if (edge.TraversalMode == EdgeTraversalMode.CarrierOnly)
      {
        if (fromNode.NodeType != NodeType.CarrierNode ||
            toNode.NodeType != NodeType.CarrierNode ||
            fromNode.ShaftId != toNode.ShaftId)
        {
          errors.Add(new TopologyValidationError(
              TopologyValidationErrorCode.InvalidCarrierOnlyEdge,
              $"CARRIER_ONLY edge '{edge.EdgeId}' must connect CarrierNode-to-CarrierNode within the same shaft."));
        }

        continue;
      }

      if (touchesCarrierNode)
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.InvalidCarrierNodeTraversalEdge,
            $"Edge '{edge.EdgeId}' cannot connect the level graph directly to a CarrierNode."));
        continue;
      }

      if (fromNode.LevelId != toNode.LevelId)
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.InvalidCrossLevelTraversalEdge,
            $"Edge '{edge.EdgeId}' must stay within a single level unless it is a CARRIER_ONLY edge inside a shaft."));
      }
    }
  }

  private static void ValidateDeviceBindings(
      WarehouseTopologyConfig config,
      Dictionary<NodeId, TopologyNodeConfig> nodesById,
      Dictionary<ShaftId, CarrierShaftConfig> shaftsById,
      List<TopologyValidationError> errors)
  {
    foreach (var binding in config.DeviceBindings)
    {
      ValidateOptionalNodeReference(binding.DeviceId, "initial node", binding.InitialNodeId, nodesById, errors);
      ValidateOptionalNodeReference(binding.DeviceId, "home node", binding.HomeNodeId, nodesById, errors);

      if (binding.Family == DeviceFamily.HybridLift &&
          binding.ShaftId is { } shaftId &&
          !shaftsById.TryGetValue(shaftId, out var shaft))
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.InvalidDeviceBindingShaftReference,
            $"HybridLift '{binding.DeviceId}' references unknown shaft '{shaftId}'."));
      }
      else if (binding.Family == DeviceFamily.HybridLift &&
               binding.ShaftId is { } existingShaftId &&
               shaftsById.TryGetValue(existingShaftId, out var existingShaft) &&
               existingShaft.CarrierDeviceId != binding.DeviceId)
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.InvalidDeviceBindingShaftReference,
            $"HybridLift '{binding.DeviceId}' must match shaft '{existingShaftId}' carrier device binding."));
      }
    }
  }

  private static void ValidateEndpointMappings(
      WarehouseTopologyConfig config,
      Dictionary<StationId, StationConfig> stationsById,
      Dictionary<ServicePointId, ServicePointConfig> servicePointsById,
      Dictionary<DeviceId, DeviceBindingConfig> deviceBindingsById,
      List<TopologyValidationError> errors)
  {
    var deviceIds = deviceBindingsById.Keys
        .Select(static deviceId => deviceId.Value)
        .ToHashSet(StringComparer.Ordinal);

    foreach (var endpointMapping in config.EndpointMappings)
    {
      if (deviceIds.Contains(endpointMapping.EndpointId.Value))
      {
        errors.Add(new TopologyValidationError(
            TopologyValidationErrorCode.EndpointIdConflictsWithDeviceId,
            $"Endpoint '{endpointMapping.EndpointId}' must not reuse an existing device identifier."));
      }

      switch (endpointMapping.EndpointKind)
      {
        case EndpointKind.LoadStation:
        case EndpointKind.UnloadStation:
          if (endpointMapping.StationId is { } stationId && !stationsById.TryGetValue(stationId, out _))
          {
            errors.Add(new TopologyValidationError(
                TopologyValidationErrorCode.InvalidEndpointReference,
                $"Endpoint '{endpointMapping.EndpointId}' references unknown station '{stationId}'."));
          }
          else if (endpointMapping.StationId is { } existingStationId &&
                   stationsById.TryGetValue(existingStationId, out var existingStation) &&
                   !MatchesStationEndpointKind(endpointMapping.EndpointKind, existingStation.StationType))
          {
            errors.Add(new TopologyValidationError(
                TopologyValidationErrorCode.InvalidEndpointTargetType,
                $"Endpoint '{endpointMapping.EndpointId}' kind '{endpointMapping.EndpointKind}' is incompatible with station '{existingStationId}' type '{existingStation.StationType}'."));
          }

          break;
        case EndpointKind.ChargePoint:
        case EndpointKind.ServicePoint:
          if (endpointMapping.ServicePointId is { } servicePointId && !servicePointsById.TryGetValue(servicePointId, out _))
          {
            errors.Add(new TopologyValidationError(
                TopologyValidationErrorCode.InvalidEndpointReference,
                $"Endpoint '{endpointMapping.EndpointId}' references unknown service point '{servicePointId}'."));
          }
          else if (endpointMapping.ServicePointId is { } existingServicePointId &&
                   servicePointsById.TryGetValue(existingServicePointId, out var existingServicePoint) &&
                   !MatchesServicePointEndpointKind(endpointMapping.EndpointKind, existingServicePoint.ServicePointType))
          {
            errors.Add(new TopologyValidationError(
                TopologyValidationErrorCode.InvalidEndpointTargetType,
                $"Endpoint '{endpointMapping.EndpointId}' kind '{endpointMapping.EndpointKind}' is incompatible with service point '{existingServicePointId}' type '{existingServicePoint.ServicePointType}'."));
          }

          break;
        default:
          throw new InvalidOperationException($"Unsupported endpoint kind '{endpointMapping.EndpointKind}'.");
      }
    }
  }

  private static bool MatchesStationEndpointKind(EndpointKind endpointKind, StationType stationType) =>
      (endpointKind, stationType) switch
      {
        (EndpointKind.LoadStation, StationType.Load) => true,
        (EndpointKind.UnloadStation, StationType.Unload) => true,
        _ => false
      };

  private static bool MatchesServicePointEndpointKind(EndpointKind endpointKind, ServicePointType servicePointType) =>
      (endpointKind, servicePointType) switch
      {
        (EndpointKind.ChargePoint, ServicePointType.Charge) => true,
        (EndpointKind.ServicePoint, ServicePointType.Service) => true,
        _ => false
      };

  private static void ValidateShaftStopNode(
      ShaftId shaftId,
      LevelId levelId,
      NodeId nodeId,
      NodeType expectedNodeType,
      string nodeRole,
      Dictionary<NodeId, TopologyNodeConfig> nodesById,
      List<TopologyValidationError> errors)
  {
    if (!nodesById.TryGetValue(nodeId, out var node))
    {
      errors.Add(new TopologyValidationError(
          TopologyValidationErrorCode.UnknownNodeReference,
          $"Shaft '{shaftId}' references unknown {nodeRole} '{nodeId}'."));
      return;
    }

    if (node.NodeType != expectedNodeType || node.ShaftId != shaftId || node.LevelId != levelId)
    {
      errors.Add(new TopologyValidationError(
          TopologyValidationErrorCode.InvalidShaftStop,
          $"Shaft '{shaftId}' {nodeRole} '{nodeId}' must match node type '{expectedNodeType}', shaft '{shaftId}', and level '{levelId}'."));
    }
  }

  private static void ValidateOptionalNodeReference(
      DeviceId deviceId,
      string nodeRole,
      NodeId? nodeId,
      Dictionary<NodeId, TopologyNodeConfig> nodesById,
      List<TopologyValidationError> errors)
  {
    if (nodeId is null)
    {
      return;
    }

    if (!nodesById.ContainsKey(nodeId.Value))
    {
      errors.Add(new TopologyValidationError(
          TopologyValidationErrorCode.InvalidDeviceBindingNodeReference,
          $"Device '{deviceId}' references unknown {nodeRole} '{nodeId}'."));
    }
  }
}
