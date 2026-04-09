using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Application.Wcs;

public enum CarrierTransferMaterializationStatus
{
  CommandsIssued,
  AwaitingConfirmation,
  Completed,
  Suspended
}

public sealed class CarrierTransferMaterializationResult
{
  public CarrierTransferMaterializationResult(
      CarrierTransferMaterializationStatus status,
      string runtimePhase,
      IEnumerable<string>? outboxIds = null)
  {
    Status = status;
    RuntimePhase = string.IsNullOrWhiteSpace(runtimePhase)
        ? throw new ArgumentException("Runtime phase is required.", nameof(runtimePhase))
        : runtimePhase;
    OutboxIds = (outboxIds ?? Array.Empty<string>()).ToArray();
  }

  public CarrierTransferMaterializationStatus Status { get; }

  public string RuntimePhase { get; }

  public IReadOnlyList<string> OutboxIds { get; }
}

public interface IWcsCarrierTransferTaskMaterializer
{
  ValueTask<CarrierTransferMaterializationResult> MaterializeAsync(
      ExecutionTaskId executionTaskId,
      CancellationToken cancellationToken = default);
}
