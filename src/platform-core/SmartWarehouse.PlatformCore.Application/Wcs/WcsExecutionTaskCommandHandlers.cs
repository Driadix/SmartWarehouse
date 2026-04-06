using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartWarehouse.PlatformCore.Application.Contracts;
using SmartWarehouse.PlatformCore.Application.Dispatching;

namespace SmartWarehouse.PlatformCore.Application.Wcs;

internal sealed class SubmitExecutionTaskCommandHandler(IWcsExecutionTaskCommandProcessor processor)
    : IApplicationCommandHandler<SubmitExecutionTask>
{
  public ValueTask HandleAsync(SubmitExecutionTask command, CancellationToken cancellationToken = default) =>
      processor.SubmitAsync(command, cancellationToken);
}

internal sealed class CancelExecutionTaskCommandHandler(IWcsExecutionTaskCommandProcessor processor)
    : IApplicationCommandHandler<CancelExecutionTask>
{
  public ValueTask HandleAsync(CancelExecutionTask command, CancellationToken cancellationToken = default) =>
      processor.CancelAsync(command, cancellationToken);
}

public static class WcsServiceCollectionExtensions
{
  public static IServiceCollection AddWcsExecutionTaskCommandProcessing<TProcessor>(this IServiceCollection services)
      where TProcessor : class, IWcsExecutionTaskCommandProcessor
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddInProcessApplicationCommandBus();
    services.TryAddScoped<TProcessor>();
    services.TryAddScoped<IWcsExecutionTaskCommandProcessor>(serviceProvider =>
        serviceProvider.GetRequiredService<TProcessor>());
    services.AddApplicationCommandHandler<SubmitExecutionTask, SubmitExecutionTaskCommandHandler>();
    services.AddApplicationCommandHandler<CancelExecutionTask, CancelExecutionTaskCommandHandler>();

    return services;
  }
}
