using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence;

public static class PlatformCorePersistenceServiceCollectionExtensions
{
  public static IServiceCollection AddPlatformCorePersistence(this IServiceCollection services, IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);

    services.AddDbContext<PlatformCoreDbContext>(options =>
    {
      var connectionString = configuration.GetConnectionString("PlatformCore");
      if (string.IsNullOrWhiteSpace(connectionString))
      {
        throw new InvalidOperationException("Connection string 'PlatformCore' is required.");
      }

      options.UseNpgsql(
          connectionString,
          npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", PersistenceSchemas.Integration));
    });

    return services;
  }

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
