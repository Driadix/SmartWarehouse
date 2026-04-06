using SmartWarehouse.PlatformCore.Application;
using SmartWarehouse.PlatformCore.Infrastructure;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class ModuleCatalogTests
{
  [Fact]
  public void ApplicationModuleCatalogMatchesExpectedLogicalContours()
  {
    var modules = ApplicationModuleCatalog.All;

    Assert.Equal(
        ["Contracts", "Northbound", "Projections", "Topology", "Wcs", "Wes"],
        modules.Select(module => module.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());

    Assert.All(
        modules,
        module =>
        {
          Assert.StartsWith("SmartWarehouse.PlatformCore.Application.", module.RootNamespace, StringComparison.Ordinal);
          Assert.Same(typeof(ApplicationModuleCatalog).Assembly, module.MarkerType.Assembly);
        });
  }

  [Fact]
  public void InfrastructureModuleCatalogMatchesExpectedLogicalContours()
  {
    var modules = InfrastructureModuleCatalog.All;

    Assert.Equal(
        ["Messaging", "Persistence", "Projections"],
        modules.Select(module => module.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());

    Assert.All(
        modules,
        module =>
        {
          Assert.StartsWith("SmartWarehouse.PlatformCore.Infrastructure.", module.RootNamespace, StringComparison.Ordinal);
          Assert.Same(typeof(InfrastructureModuleCatalog).Assembly, module.MarkerType.Assembly);
        });
  }
}
