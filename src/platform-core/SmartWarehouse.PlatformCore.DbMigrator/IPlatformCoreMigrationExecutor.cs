namespace SmartWarehouse.PlatformCore.DbMigrator;

internal interface IPlatformCoreMigrationExecutor
{
  IReadOnlyList<string> GetKnownMigrationIds();

  Task<IReadOnlyList<string>> GetPendingMigrationIdsAsync(CancellationToken cancellationToken);

  Task ApplyMigrationsAsync(CancellationToken cancellationToken);
}
