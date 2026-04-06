using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;

namespace SmartWarehouse.PlatformCore.DbMigrator;

internal sealed class EfCorePlatformCoreMigrationExecutor(PlatformCoreDbContext dbContext) : IPlatformCoreMigrationExecutor
{
  public IReadOnlyList<string> GetKnownMigrationIds() =>
      dbContext.Database.GetMigrations().ToArray();

  public async Task<IReadOnlyList<string>> GetPendingMigrationIdsAsync(CancellationToken cancellationToken)
  {
    var pendingMigrationIds = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
    return pendingMigrationIds.ToArray();
  }

  public async Task ApplyMigrationsAsync(CancellationToken cancellationToken)
  {
    var historyRepository = dbContext.GetService<IHistoryRepository>();
    var databaseCreator = dbContext.GetService<IRelationalDatabaseCreator>();

    if (!await databaseCreator.ExistsAsync(cancellationToken))
    {
      await databaseCreator.CreateAsync(cancellationToken);
    }

    await historyRepository.CreateIfNotExistsAsync(cancellationToken);
    await dbContext.Database.MigrateAsync(cancellationToken);
  }
}
