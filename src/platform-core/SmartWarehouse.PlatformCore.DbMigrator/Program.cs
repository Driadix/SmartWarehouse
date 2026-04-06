namespace SmartWarehouse.PlatformCore.DbMigrator;

public static class Program
{
  public static async Task<int> Main(string[] args)
  {
    try
    {
      return await DbMigratorApplication.RunAsync(args);
    }
    catch (Exception exception)
    {
      Console.Error.WriteLine($"Fatal db-migrator error: {exception}");
      return (int)DbMigratorExitCode.MigrationFailed;
    }
  }
}
