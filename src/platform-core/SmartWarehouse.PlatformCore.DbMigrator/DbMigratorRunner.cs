using Microsoft.Extensions.Logging;

namespace SmartWarehouse.PlatformCore.DbMigrator;

internal sealed class DbMigratorRunner(
    IPlatformCoreMigrationExecutor migrationExecutor,
    ILogger<DbMigratorRunner> logger)
{
  public async Task<DbMigratorExitCode> RunAsync(CancellationToken cancellationToken)
  {
    try
    {
      var knownMigrationIds = migrationExecutor.GetKnownMigrationIds().ToArray();

      if (knownMigrationIds.Length == 0)
      {
        DbMigratorLog.NoKnownMigrations(logger);
        return DbMigratorExitCode.Success;
      }

      if (logger.IsEnabled(LogLevel.Information))
      {
        var knownMigrationIdsText = string.Join(", ", knownMigrationIds);

        DbMigratorLog.EnsuringDatabaseSchema(
            logger,
            knownMigrationIds.Length,
            knownMigrationIdsText);
      }

      await migrationExecutor.ApplyMigrationsAsync(cancellationToken);

      var remainingPendingMigrationIds = (await migrationExecutor.GetPendingMigrationIdsAsync(cancellationToken)).ToArray();

      if (remainingPendingMigrationIds.Length > 0)
      {
        DbMigratorLog.PendingMigrationsRemain(
            logger,
            string.Join(", ", remainingPendingMigrationIds));

        return DbMigratorExitCode.MigrationFailed;
      }

      DbMigratorLog.DatabaseSchemaUpToDate(logger, knownMigrationIds.Length);

      return DbMigratorExitCode.Success;
    }
    catch (OperationCanceledException)
    {
      DbMigratorLog.MigrationCanceled(logger);
      throw;
    }
    catch (Exception exception)
    {
      DbMigratorLog.MigrationFailed(logger, exception);
      return DbMigratorExitCode.MigrationFailed;
    }
  }
}
