using SmartWarehouse.PlatformCore.Application.Contracts;

namespace SmartWarehouse.PlatformCore.Application.Dispatching;

public interface IApplicationCommandHandler<in TCommand>
    where TCommand : class, IApplicationCommand
{
  ValueTask HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
