namespace SmartWarehouse.PlatformCore.Application.Topology;

public static class TopologyModule
{
  public static ApplicationModuleDescriptor Descriptor { get; } = new("Topology", typeof(TopologyModule));
}
