namespace SmartWarehouse.PlatformCore.Application.Contracts;

public static class ContractsModule
{
  public static ApplicationModuleDescriptor Descriptor { get; } = new("Contracts", typeof(ContractsModule));
}
