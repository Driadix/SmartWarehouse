using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Contracts;
using SmartWarehouse.PlatformCore.Application.Dispatching;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Execution;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Application.Wes;

internal sealed class WesExecutionTaskCommandGateway(IApplicationCommandBus commandBus) : IWesExecutionTaskCommandGateway
{
  public ValueTask SubmitAsync(
      ExecutionTask executionTask,
      TaskRevision taskRevision,
      EnvelopeId messageId,
      CausationId? causationId = null,
      ApplicationContractVersion? contractVersion = null,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(executionTask);

    var command = SubmitExecutionTask.FromExecutionTask(
        messageId,
        taskRevision,
        executionTask,
        causationId,
        contractVersion);

    return commandBus.SendAsync(command, cancellationToken);
  }

  public ValueTask CancelAsync(
      ExecutionTask executionTask,
      TaskRevision taskRevision,
      EnvelopeId messageId,
      ReasonCode? reasonCode = null,
      CausationId? causationId = null,
      ApplicationContractVersion? contractVersion = null,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(executionTask);

    var command = CancelExecutionTask.ForExecutionTask(
        messageId,
        taskRevision,
        executionTask,
        reasonCode,
        causationId,
        contractVersion);

    return commandBus.SendAsync(command, cancellationToken);
  }
}

public static class WesServiceCollectionExtensions
{
  public static IServiceCollection AddWesExecutionTaskCommandGateway(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddInProcessApplicationCommandBus();
    services.AddScoped<IWesExecutionTaskCommandGateway, WesExecutionTaskCommandGateway>();

    return services;
  }
}
