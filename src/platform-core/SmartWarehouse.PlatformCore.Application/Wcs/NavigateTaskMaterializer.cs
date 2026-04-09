using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Application.Wcs;

public enum NavigateMaterializationStatus
{
  MotionAuthorized,
  AlreadyAuthorized,
  Completed,
  Suspended
}

public sealed class NavigateMaterializationResult
{
  public NavigateMaterializationResult(
      NavigateMaterializationStatus status,
      IEnumerable<NodeId>? authorizedNodePath = null,
      string? outboxId = null)
  {
    Status = status;
    AuthorizedNodePath = (authorizedNodePath ?? Array.Empty<NodeId>()).ToArray();
    OutboxId = outboxId;
  }

  public NavigateMaterializationStatus Status { get; }

  public IReadOnlyList<NodeId> AuthorizedNodePath { get; }

  public string? OutboxId { get; }
}

public interface IWcsNavigateTaskMaterializer
{
  ValueTask<NavigateMaterializationResult> MaterializeAsync(
      ExecutionTaskId executionTaskId,
      CancellationToken cancellationToken = default);
}
