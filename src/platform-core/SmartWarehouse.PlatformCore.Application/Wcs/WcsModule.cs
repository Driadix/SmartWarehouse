namespace SmartWarehouse.PlatformCore.Application.Wcs;

public static class WcsModule
{
  public static ApplicationModuleDescriptor Descriptor { get; } = new("Wcs", typeof(WcsModule));
}
