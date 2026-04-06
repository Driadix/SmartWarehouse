using Microsoft.Extensions.Logging;

namespace SmartWarehouse.PlatformCore.DbMigrator;

internal static partial class DbMigratorLog
{
  [LoggerMessage(
      EventId = 1000,
      Level = LogLevel.Information,
      Message = "No known EF Core migrations were found for PlatformCoreDbContext.")]
  public static partial void NoKnownMigrations(ILogger logger);

  [LoggerMessage(
      EventId = 1001,
      Level = LogLevel.Information,
      Message = "Ensuring database schema is up to date using {KnownMigrationCount} known migrations: {KnownMigrationIds}.")]
  public static partial void EnsuringDatabaseSchema(
      ILogger logger,
      int knownMigrationCount,
      string knownMigrationIds);

  [LoggerMessage(
      EventId = 1002,
      Level = LogLevel.Information,
      Message = "Database schema is up to date. Verified {KnownMigrationCount} known migrations.")]
  public static partial void DatabaseSchemaUpToDate(ILogger logger, int knownMigrationCount);

  [LoggerMessage(
      EventId = 1003,
      Level = LogLevel.Warning,
      Message = "Database migration was canceled.")]
  public static partial void MigrationCanceled(ILogger logger);

  [LoggerMessage(
      EventId = 1004,
      Level = LogLevel.Error,
      Message = "Database migration failed.")]
  public static partial void MigrationFailed(ILogger logger, Exception exception);

  [LoggerMessage(
      EventId = 1005,
      Level = LogLevel.Error,
      Message = "Database migration finished, but pending migrations still remain: {PendingMigrationIds}.")]
  public static partial void PendingMigrationsRemain(ILogger logger, string pendingMigrationIds);
}
