namespace SmartWarehouse.PlatformCore.Infrastructure.Projections;

public static class InfrastructureProjectionModule
{
  public static InfrastructureModuleDescriptor Descriptor { get; } = new("Projections", typeof(InfrastructureProjectionModule));
}
