using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Application.Wcs;

public enum StationTransferMaterializationStatus
{
  BoundaryMotionAuthorized,
  AlreadyAuthorized,
  BoundaryPositionConfirmed,
  Completed,
  Suspended
}

public sealed class StationTransferMaterializationResult
{
  public StationTransferMaterializationResult(
      StationTransferMaterializationStatus status,
      IEnumerable<NodeId>? authorizedNodePath = null,
      string? outboxId = null)
  {
    Status = status;
    AuthorizedNodePath = (authorizedNodePath ?? Array.Empty<NodeId>()).ToArray();
    OutboxId = outboxId;
  }

  public StationTransferMaterializationStatus Status { get; }

  public IReadOnlyList<NodeId> AuthorizedNodePath { get; }

  public string? OutboxId { get; }
}

public interface IWcsStationTransferTaskMaterializer
{
  ValueTask<StationTransferMaterializationResult> MaterializeAsync(
      ExecutionTaskId executionTaskId,
      CancellationToken cancellationToken = default);

  ValueTask<StationTransferMaterializationResult> ConfirmTransferAsync(
      ExecutionTaskId executionTaskId,
      PayloadId payloadId,
      CancellationToken cancellationToken = default);
}
