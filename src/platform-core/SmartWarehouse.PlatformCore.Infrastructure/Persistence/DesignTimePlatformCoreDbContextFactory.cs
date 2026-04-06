using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence;

public sealed class DesignTimePlatformCoreDbContextFactory : IDesignTimeDbContextFactory<PlatformCoreDbContext>
{
  private const string DefaultConnectionString =
      "Host=localhost;Port=5432;Database=smartwarehouse;Username=smartwarehouse;Password=smartwarehouse";

  public PlatformCoreDbContext CreateDbContext(string[] args)
  {
    var connectionString =
        Environment.GetEnvironmentVariable("ConnectionStrings__PlatformCore")
        ?? Environment.GetEnvironmentVariable("PlatformCore__ConnectionString")
        ?? DefaultConnectionString;

    var options = new DbContextOptionsBuilder<PlatformCoreDbContext>()
        .UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", PersistenceSchemas.Integration))
        .Options;

    return new PlatformCoreDbContext(options);
  }
}
