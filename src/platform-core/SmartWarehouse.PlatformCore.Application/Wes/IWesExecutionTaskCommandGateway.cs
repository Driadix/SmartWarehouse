using SmartWarehouse.PlatformCore.Application.Contracts;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Execution;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Application.Wes;

public interface IWesExecutionTaskCommandGateway
{
  ValueTask SubmitAsync(
      ExecutionTask executionTask,
      TaskRevision taskRevision,
      EnvelopeId messageId,
      CausationId? causationId = null,
      ApplicationContractVersion? contractVersion = null,
      CancellationToken cancellationToken = default);

  ValueTask CancelAsync(
      ExecutionTask executionTask,
      TaskRevision taskRevision,
      EnvelopeId messageId,
      ReasonCode? reasonCode = null,
      CausationId? causationId = null,
      ApplicationContractVersion? contractVersion = null,
      CancellationToken cancellationToken = default);
}
