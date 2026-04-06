namespace SmartWarehouse.PlatformCore.DbMigrator;

public enum DbMigratorExitCode
{
  Success = 0,
  MissingConnectionString = 10,
  MigrationFailed = 20
}
