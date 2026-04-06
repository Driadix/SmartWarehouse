namespace SmartWarehouse.PlatformCore.Application;

public static class ApplicationModuleCatalog
{
  public static IReadOnlyList<ApplicationModuleDescriptor> All { get; } =
  [
      Contracts.ContractsModule.Descriptor,
      Northbound.NorthboundModule.Descriptor,
      Projections.ApplicationProjectionModule.Descriptor,
      Topology.TopologyModule.Descriptor,
      Wes.WesModule.Descriptor,
      Wcs.WcsModule.Descriptor
  ];
}
