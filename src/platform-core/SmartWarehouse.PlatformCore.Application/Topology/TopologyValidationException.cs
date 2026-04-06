using System.Collections.ObjectModel;

namespace SmartWarehouse.PlatformCore.Application.Topology;

public sealed class TopologyValidationException : Exception
{
  public TopologyValidationException(IEnumerable<TopologyValidationError> errors)
      : base(CreateMessage(errors))
  {
    Errors = CreateReadOnlyErrors(errors);
  }

  public IReadOnlyList<TopologyValidationError> Errors { get; }

  private static ReadOnlyCollection<TopologyValidationError> CreateReadOnlyErrors(IEnumerable<TopologyValidationError> errors)
  {
    ArgumentNullException.ThrowIfNull(errors);

    var materialized = errors.ToArray();

    if (materialized.Length == 0)
    {
      throw new ArgumentException("Validation error collection cannot be empty.", nameof(errors));
    }

    return Array.AsReadOnly(materialized);
  }

  private static string CreateMessage(IEnumerable<TopologyValidationError> errors)
  {
    var materialized = CreateReadOnlyErrors(errors);
    var details = string.Join(Environment.NewLine, materialized.Select(static error => $"{error.Code}: {error.Message}"));
    return $"Topology configuration validation failed.{Environment.NewLine}{details}";
  }
}

public readonly record struct TopologyValidationError(TopologyValidationErrorCode Code, string Message);

public enum TopologyValidationErrorCode
{
  UnknownLevelReference,
  UnknownNodeReference,
  UnknownStationReference,
  UnknownServicePointReference,
  UnknownShaftReference,
  InvalidStationAttachment,
  InvalidServicePointNode,
  InvalidShaftCarrierDeviceBinding,
  InvalidShaftStop,
  DuplicateCarrierNodeLevel,
  MissingTransferPointForCarrierNode,
  InvalidCarrierOnlyEdge,
  InvalidCarrierNodeTraversalEdge,
  InvalidCrossLevelTraversalEdge,
  InvalidDeviceBindingNodeReference,
  InvalidDeviceBindingShaftReference,
  InvalidEndpointReference,
  InvalidEndpointTargetType,
  EndpointIdConflictsWithDeviceId
}
