using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartWarehouse.PlatformCore.Application.Contracts;

namespace SmartWarehouse.PlatformCore.Application.Dispatching;

public static class ApplicationCommandBusServiceCollectionExtensions
{
  public static IServiceCollection AddInProcessApplicationCommandBus(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.TryAddScoped<InProcessApplicationCommandBus>();
    services.TryAddScoped<IApplicationCommandBus>(serviceProvider =>
        serviceProvider.GetRequiredService<InProcessApplicationCommandBus>());

    return services;
  }

  public static IServiceCollection AddApplicationCommandHandler<TCommand, THandler>(this IServiceCollection services)
      where TCommand : class, IApplicationCommand
      where THandler : class, IApplicationCommandHandler<TCommand>
  {
    ArgumentNullException.ThrowIfNull(services);

    services.TryAddEnumerable(ServiceDescriptor.Scoped<IApplicationCommandHandler<TCommand>, THandler>());

    return services;
  }
}
