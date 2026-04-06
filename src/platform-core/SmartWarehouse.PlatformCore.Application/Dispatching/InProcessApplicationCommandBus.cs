using Microsoft.Extensions.DependencyInjection;

namespace SmartWarehouse.PlatformCore.Application.Dispatching;

internal sealed class InProcessApplicationCommandBus(IServiceProvider serviceProvider) : IApplicationCommandBus
{
  public ValueTask SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
      where TCommand : class, Contracts.IApplicationCommand
  {
    ArgumentNullException.ThrowIfNull(command);

    var handlers = serviceProvider.GetServices<IApplicationCommandHandler<TCommand>>().ToArray();

    return handlers.Length switch
    {
      1 => handlers[0].HandleAsync(command, cancellationToken),
      0 => throw new InvalidOperationException(
          $"No application command handler is registered for '{typeof(TCommand).FullName}'."),
      _ => throw new InvalidOperationException(
          $"Exactly one application command handler must be registered for '{typeof(TCommand).FullName}', but {handlers.Length} were found.")
    };
  }
}
