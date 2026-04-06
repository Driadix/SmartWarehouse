namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence;

public static class PersistenceSchemas
{
  public const string Config = "config";
  public const string Wes = "wes";
  public const string Wcs = "wcs";
  public const string Integration = "integration";
  public const string Projection = "projection";
  public const string Audit = "audit";

  public static IReadOnlyList<string> All { get; } =
  [
      Config,
      Wes,
      Wcs,
      Integration,
      Projection,
      Audit
  ];
}
