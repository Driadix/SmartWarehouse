using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence;

public static class PlatformCorePersistenceServiceCollectionExtensions
{
  public static IServiceCollection AddPlatformCorePersistence(this IServiceCollection services, string connectionString)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

    services.AddDbContext<PlatformCoreDbContext>(options =>
    {
      options.UseNpgsql(
          connectionString,
          npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", PersistenceSchemas.Integration));
    });

    return services;
  }
}
