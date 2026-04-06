namespace SmartWarehouse.PlatformCore.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class PlatformCoreIntegrationFixtureDefinition : ICollectionFixture<PlatformCoreTestcontainersHarness>
{
  public const string Name = "PlatformCore integration collection";
}
