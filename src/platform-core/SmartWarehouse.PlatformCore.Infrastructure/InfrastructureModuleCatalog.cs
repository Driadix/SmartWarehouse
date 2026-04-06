namespace SmartWarehouse.PlatformCore.Infrastructure;

public static class InfrastructureModuleCatalog
{
  public static IReadOnlyList<InfrastructureModuleDescriptor> All { get; } =
  [
      Messaging.MessagingModule.Descriptor,
      Persistence.PersistenceModule.Descriptor,
      Projections.InfrastructureProjectionModule.Descriptor
  ];
}
