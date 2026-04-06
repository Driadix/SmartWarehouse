using SmartWarehouse.PlatformCore.Application.Contracts;

namespace SmartWarehouse.PlatformCore.Application.Wcs;

public interface IWcsExecutionTaskCommandProcessor
{
  ValueTask SubmitAsync(SubmitExecutionTask command, CancellationToken cancellationToken = default);

  ValueTask CancelAsync(CancelExecutionTask command, CancellationToken cancellationToken = default);
}
