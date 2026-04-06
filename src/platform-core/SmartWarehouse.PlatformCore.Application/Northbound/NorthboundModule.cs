namespace SmartWarehouse.PlatformCore.Application.Northbound;

public static class NorthboundModule
{
  public static ApplicationModuleDescriptor Descriptor { get; } = new("Northbound", typeof(NorthboundModule));
}
