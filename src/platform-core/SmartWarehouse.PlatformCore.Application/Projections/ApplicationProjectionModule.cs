namespace SmartWarehouse.PlatformCore.Application.Projections;

public static class ApplicationProjectionModule
{
  public static ApplicationModuleDescriptor Descriptor { get; } = new("Projections", typeof(ApplicationProjectionModule));
}
