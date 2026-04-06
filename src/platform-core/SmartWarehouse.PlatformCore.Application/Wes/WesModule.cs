namespace SmartWarehouse.PlatformCore.Application.Wes;

public static class WesModule
{
  public static ApplicationModuleDescriptor Descriptor { get; } = new("Wes", typeof(WesModule));
}
