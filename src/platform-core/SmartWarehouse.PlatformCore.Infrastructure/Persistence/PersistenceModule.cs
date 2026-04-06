namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence;

public static class PersistenceModule
{
  public static InfrastructureModuleDescriptor Descriptor { get; } = new("Persistence", typeof(PersistenceModule));
}
