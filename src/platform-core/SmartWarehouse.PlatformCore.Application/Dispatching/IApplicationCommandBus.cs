using SmartWarehouse.PlatformCore.Application.Contracts;

namespace SmartWarehouse.PlatformCore.Application.Dispatching;

public interface IApplicationCommandBus
{
  ValueTask SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
      where TCommand : class, IApplicationCommand;
}
